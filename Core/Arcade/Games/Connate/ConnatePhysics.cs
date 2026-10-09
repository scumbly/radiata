namespace ControllerWheel;

/// <summary>Deterministic rank-scaled-circle center-gravity simulation and merge arbitration.</summary>
public static class ConnatePhysics
{
    // Candidate is ID-based: a substep may discover the same overlap across several solver iterations, and
    // only its deepest penetration is retained for deterministic merge arbitration.
    private readonly record struct Candidate(long A, long B, double Penetration);
    private readonly record struct Contact(double NormalX, double NormalY, double Penetration);
    private readonly record struct ConvexPart((double X, double Y)[] Points);
    private readonly record struct ColliderGeometry(bool IsCircle, double BroadRadius, ConvexPart[] Parts);

    /// <summary>Advances one host simulation slice. Callers own the body/pending lists and the monotonic ID
    /// counter. The returned merge and garbage-clear events are presentation notifications only; all logical
    /// mutations have already happened before this returns.</summary>
    public static List<ConnateMergeEvent> Step(List<ConnateBody> bodies, List<ConnatePendingMerge> pending,
                                               double dt, ref long nextBodyId,
                                               List<ConnateGarbageClear>? garbageClears = null)
    {
        var events = new List<ConnateMergeEvent>();
        int substeps = Math.Clamp(ConnateTuning.PhysicsSubsteps, 1, 8);
        double h = dt / substeps;
        var candidates = new Dictionary<(long, long), Candidate>();
        var colliders = new ColliderGeometry[bodies.Count];

        // Stable ID ordering is part of replay determinism. Do not replace with spatial enumeration unless the
        // replacement defines an equally stable pair order and the snapshot/determinism probes are updated.
        for (int substep = 0; substep < substeps; substep++)
        {
            bodies.Sort((a, b) => a.Id.CompareTo(b.Id));
            Integrate(bodies, h);

            // Collision is iterated for positional convergence; candidate collection spans those iterations.
            candidates.Clear();
            // Local sector vertices stay valid while positional correction moves body centers, so rebuild once
            // per substep — deterministic, and still picks up live tuning changes to radius/segment count.
            for (int index = 0; index < bodies.Count; index++) colliders[index] = BuildCollider(bodies[index]);
            int iterations = Math.Clamp(ConnateTuning.CollisionIterations, 1, 6);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                Collide(bodies, colliders, candidates);
                Contain(bodies);
            }

            UpdatePendingMerges(bodies, pending, candidates.Values, h);
            ResolveMerges(bodies, pending, events, ref nextBodyId, garbageClears);
        }
        return events;
    }

    private static void Integrate(List<ConnateBody> bodies, double dt)
    {
        double damping = Math.Exp(-Math.Max(0, ConnateTuning.LinearDragPerSec) * dt);
        double ordinaryMax = Math.Max(0.1, ConnateTuning.MaxSpeedPerSec);
        double chargedMax = Math.Max(ordinaryMax, ConnateTuning.ChargedMaxSpeedPerSec);
        foreach (ConnateBody body in bodies)
        {
            double maxSpeed = body.Charged ? chargedMax : ordinaryMax;
            double maxSpeedSquared = maxSpeed * maxSpeed;
            body.MergeLock = Math.Max(0, body.MergeLock - dt);
            body.JellyTime = Math.Max(0, body.JellyTime - dt);
            body.Age += dt;
            // The platter: a rigid clockwise turn of the whole heap about the centre — position, velocity and
            // the body's drawn rotation together. Rigid means every pair's relative position is untouched, so
            // contacts, merges and the clump envelope see exactly what they saw before.
            //
            // ⚠ Distinct from the swirl below, which applies a tangential acceleration that gravity and
            // collision damping absorb almost entirely. The swirl supplies loose drift between neighbours;
            // this supplies the visible turn. Don't try to replace one with the other.
            double platter = ConnateTuning.PlatterDegreesPerSecond * Math.PI / 180.0 * dt;
            if (platter != 0)
            {
                double cos = Math.Cos(platter), sin = Math.Sin(platter);
                (body.X, body.Y) = (body.X * cos - body.Y * sin, body.X * sin + body.Y * cos);
                (body.VelocityX, body.VelocityY) =
                    (body.VelocityX * cos - body.VelocityY * sin, body.VelocityX * sin + body.VelocityY * cos);
                body.Rotation += platter;
            }
            // Fired low-rank tiles spin, damping out as they settle into the heap.
            if (body.SpinRate != 0)
            {
                body.Rotation += body.SpinRate * dt;
                body.SpinRate *= Math.Max(0, 1 - ConnateTuning.PieceSpinDampingPerSec * dt);
                if (Math.Abs(body.SpinRate) < 1e-4) body.SpinRate = 0;
            }
            // Linear center gravity avoids an inverse-square singularity. (-Y,+X) is clockwise in screen space
            // because screen Y grows downward; making the swirl proportional to radius keeps the center calm.
            body.VelocityX += -ConnateTuning.GravityPerSec2 * body.X * dt;
            body.VelocityY += -ConnateTuning.GravityPerSec2 * body.Y * dt;
            body.VelocityX += -body.Y * ConnateTuning.ClockwiseSwirlPerSec2 * dt;
            body.VelocityY += body.X * ConnateTuning.ClockwiseSwirlPerSec2 * dt;
            ApplyOuterCushion(body, dt);
            body.VelocityX *= damping;
            body.VelocityY *= damping;

            double speedSquared = body.VelocityX * body.VelocityX + body.VelocityY * body.VelocityY;
            if (speedSquared > maxSpeedSquared)
            {
                double scale = maxSpeed / Math.Sqrt(speedSquared);
                body.VelocityX *= scale;
                body.VelocityY *= scale;
            }

            body.X += body.VelocityX * dt;
            body.Y += body.VelocityY * dt;
            // Incoming garbage alone may begin beyond the hard wall. The exemption is permanently removed as
            // soon as its outer edge crosses inside; all subsequent blasts/collisions use normal containment.
            if (body.EnteringPlayfield
                && body.RadiusFromCenter + body.Radius <= ConnateTuning.OuterHardLimitRadius)
                body.EnteringPlayfield = false;
            Contain(body);
            // Fresh shots do not threaten the size limit while crossing the rim. Contact with an armed body can
            // also arm them in Collide, which prevents a blocked outer shot from remaining exempt forever.
            if (!body.SizeArmed && body.RadiusFromCenter + body.Radius <= ConnateTuning.ClumpLimitRadius)
                body.SizeArmed = true;
        }
    }

    public static void ApplyBlast(List<ConnateBody> bodies, double x, double y)
    {
        double radius = Math.Max(0.01, ConnateTuning.BombBlastRadius);
        foreach (ConnateBody body in bodies)
        {
            double dx = body.X - x, dy = body.Y - y;
            double distance = new Vec2(dx, dy).Length;
            if (distance >= radius) continue;
            double nx, ny;
            if (distance < 1e-6)
            {
                double angle = DegenerateAngle(body.Id, body.Id + 17);
                nx = Math.Cos(angle); ny = Math.Sin(angle);
            }
            else { nx = dx / distance; ny = dy / distance; }
            // Squared falloff gives a punchy epicenter without making the whole heap translate as one block.
            double t = 1 - distance / radius;
            double impulse = ConnateTuning.BombBlastImpulse
                * Math.Pow(t, Math.Max(0.25, ConnateTuning.BombBlastFalloffExponent));
            body.VelocityX += nx * impulse;
            body.VelocityY += ny * impulse;
        }
    }

    public static void Contain(List<ConnateBody> bodies)
    {
        foreach (ConnateBody body in bodies) Contain(body);
    }

    private static void ApplyOuterCushion(ConnateBody body, double dt)
    {
        if (body.EnteringPlayfield) return;
        double distance = body.RadiusFromCenter;
        double edge = distance + body.Radius;
        if (distance < 1e-8 || edge <= ConnateTuning.OuterCushionStartRadius) return;
        double nx = body.X / distance, ny = body.Y / distance;
        double penetration = edge - ConnateTuning.OuterCushionStartRadius;
        double radialVelocity = body.VelocityX * nx + body.VelocityY * ny;
        // The spring scales with edge penetration; damping applies only to outward radial motion so returning
        // bodies are not artificially slowed on their way back to the heap.
        double acceleration = ConnateTuning.OuterCushionStrength * penetration
            + Math.Max(0, radialVelocity) * ConnateTuning.OuterRadialDamping;
        body.VelocityX -= nx * acceleration * dt;
        body.VelocityY -= ny * acceleration * dt;
    }

    private static void Contain(ConnateBody body)
    {
        if (body.EnteringPlayfield) return;
        double allowed = Math.Max(0.05, ConnateTuning.OuterHardLimitRadius - body.Radius);
        double distance = body.RadiusFromCenter;
        if (distance <= allowed || distance < 1e-8) return;
        double nx = body.X / distance, ny = body.Y / distance;
        // The non-negotiable safety layer after the soft cushion: clamp the center by body radius so the whole
        // shape stays visible, then reflect only the outward velocity component.
        body.X = nx * allowed; body.Y = ny * allowed;
        double outward = body.VelocityX * nx + body.VelocityY * ny;
        if (outward > 0)
        {
            body.VelocityX -= nx * outward * 1.38;
            body.VelocityY -= ny * outward * 1.38;
        }
    }

    private static void Collide(List<ConnateBody> bodies, ColliderGeometry[] colliders,
                                Dictionary<(long, long), Candidate> candidates)
    {
        double slop = Math.Max(0, ConnateTuning.CollisionSlop);

        for (int i = 0; i < bodies.Count; i++)
        for (int j = i + 1; j < bodies.Count; j++)
        {
            ConnateBody a = bodies[i], b = bodies[j];
            ref readonly var colliderA = ref colliders[i];
            ref readonly var colliderB = ref colliders[j];
            double radiusA = colliderA.BroadRadius, radiusB = colliderB.BroadRadius;
            double contactDistance = radiusA + radiusB;
            double contactDistanceSquared = contactDistance * contactDistance;
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double distanceSquared = dx * dx + dy * dy;
            if (distanceSquared >= contactDistanceSquared) continue;

            if (!TryContact(a, b, colliderA, colliderB, out Contact contact)) continue;
            double nx = contact!.NormalX, ny = contact.NormalY;
            double penetration = contact.Penetration;
            // Mass is proportional to visible area (r²). Larger high-rank circles therefore yield less to the
            // same overlap or impulse than base-rank circles. Sector pieces use their occupied area fraction,
            // making 1 + 2 carry exactly one 3-disc worth of combined collision mass.
            double inverseMassA = 1.0 / Math.Max(1e-8, a.Mass);
            double inverseMassB = 1.0 / Math.Max(1e-8, b.Mass);
            double inverseMassSum = inverseMassA + inverseMassB;
            double correction = Math.Max(0, penetration - slop) * ConnateTuning.PositionCorrection;
            double correctionA = correction * inverseMassA / inverseMassSum;
            double correctionB = correction * inverseMassB / inverseMassSum;
            a.X -= nx * correctionA; a.Y -= ny * correctionA;
            b.X += nx * correctionB; b.Y += ny * correctionB;

            double relativeX = b.VelocityX - a.VelocityX;
            double relativeY = b.VelocityY - a.VelocityY;
            double closing = relativeX * nx + relativeY * ny;
            if (closing < 0)
            {
                // ⚠ The flag is read and cleared in this one block, on both bodies, so the first closing contact
                // is the only one that gets the slam bounce — the second solver pass and the second substep see
                // plain bodies. Resting or separating overlaps never reach here and keep the flag.
                double restitution = a.Charged || b.Charged
                    ? ConnateTuning.ChargedImpactRestitution : ConnateTuning.Restitution;
                a.Charged = b.Charged = false;
                double impulse = -(1 + restitution) * closing / inverseMassSum;
                a.VelocityX -= impulse * inverseMassA * nx; a.VelocityY -= impulse * inverseMassA * ny;
                b.VelocityX += impulse * inverseMassB * nx; b.VelocityY += impulse * inverseMassB * ny;
            }

            double tx = -ny, ty = nx;
            double tangentRelative = relativeX * tx + relativeY * ty;
            double tangentImpulse = tangentRelative * ConnateTuning.TangentialDamping / inverseMassSum;
            a.VelocityX += tangentImpulse * inverseMassA * tx; a.VelocityY += tangentImpulse * inverseMassA * ty;
            b.VelocityX -= tangentImpulse * inverseMassB * tx; b.VelocityY -= tangentImpulse * inverseMassB * ty;

            // Size eligibility spreads through contact. Garbage collides normally but is explicitly excluded
            // from arithmetic candidate creation; bombs also ignore it in Connate.StepBombs.
            if (a.SizeArmed || b.SizeArmed) { a.SizeArmed = true; b.SizeArmed = true; }
            if (a.IsGarbage || b.IsGarbage || a.MergeLock > 0 || b.MergeLock > 0
                || !ConnateRules.Compatible(a.Rank, a.Hue, b.Rank, b.Hue)) continue;   // same family only

            var key = a.Id < b.Id ? (a.Id, b.Id) : (b.Id, a.Id);
            if (!candidates.TryGetValue(key, out Candidate old) || penetration > old.Penetration)
                candidates[key] = new Candidate(key.Item1, key.Item2, penetration);
        }
    }

    /// <summary>Whether a pending bond between <paramref name="a"/> and <paramref name="b"/> is still a legal
    /// merge: neither is garbage, the pair is arithmetically and family compatible, and the bond's recorded
    /// result is the rank that pair actually produces (which also keeps the result's family derivable and
    /// the rank on the ladder). Checked every substep and on every restored bond; reservation uniqueness is the
    /// caller's, since it needs the whole list.</summary>
    public static bool IsMergeValid(ConnatePendingMerge bond, ConnateBody a, ConnateBody b) =>
        a.Id != b.Id && !a.IsGarbage && !b.IsGarbage
        && ConnateRules.Compatible(a.Rank, a.Hue, b.Rank, b.Hue)
        && ConnateRules.MergeResultRank(a.Rank, b.Rank) == bond.ResultRank;

    private static void UpdatePendingMerges(List<ConnateBody> bodies, List<ConnatePendingMerge> pending,
                                            IEnumerable<Candidate> source, double dt)
    {
        var byId = bodies.ToDictionary(body => body.Id);
        // A reserved bond survives small collision separation, but is cancelled if either endpoint disappears,
        // is not a legal merge (result, family, garbage), is pulled materially apart before its
        // anticipation timer matures, or claims a body an earlier bond already holds.
        var claimed = new HashSet<long>();
        pending.RemoveAll(bond =>
        {
            if (bond.AId == bond.BId
                || !byId.TryGetValue(bond.AId, out ConnateBody? a)
                || !byId.TryGetValue(bond.BId, out ConnateBody? b)
                || !IsMergeValid(bond, a, b)
                || Distance(a, b) > (a.Radius + b.Radius) * 1.40) return true;
            if (claimed.Contains(bond.AId) || claimed.Contains(bond.BId)) return true;
            claimed.Add(bond.AId); claimed.Add(bond.BId);
            return false;
        });

        // Deepest penetration wins, then IDs break ties. One body can belong to only one bond at a time.
        var reserved = pending.SelectMany(bond => new[] { bond.AId, bond.BId }).ToHashSet();
        foreach (Candidate candidate in source.OrderByDescending(c => c.Penetration).ThenBy(c => c.A).ThenBy(c => c.B))
        {
            if (reserved.Contains(candidate.A) || reserved.Contains(candidate.B)) continue;
            if (!byId.TryGetValue(candidate.A, out ConnateBody? a)
                || !byId.TryGetValue(candidate.B, out ConnateBody? b)) continue;
            int resultRank = ConnateRules.MergeResultRank(a.Rank, b.Rank);
            if (resultRank < 0 || a.MergeLock > 0 || b.MergeLock > 0) continue;

            double stagger = ((candidate.A ^ candidate.B) & 3) * 0.006;
            pending.Add(new ConnatePendingMerge
            {
                AId = candidate.A,
                BId = candidate.B,
                ResultRank = resultRank,
                Duration = Math.Max(0.04, ConnateTuning.MergeAnticipationSeconds + stagger),
            });
            reserved.Add(candidate.A);
            reserved.Add(candidate.B);
        }

        // Bond forces gather the pair and align velocities, so the replacement body doesn't inject a burst of
        // kinetic energy.
        double bondDamping = 1 - Math.Exp(-Math.Max(0, ConnateTuning.MergeBondDampingPerSec) * dt);
        foreach (ConnatePendingMerge bond in pending)
        {
            bond.Age += dt;
            if (!byId.TryGetValue(bond.AId, out ConnateBody? a)
                || !byId.TryGetValue(bond.BId, out ConnateBody? b)) continue;

            double dx = b.X - a.X, dy = b.Y - a.Y;
            double distance = Math.Sqrt(Math.Max(1e-12, dx * dx + dy * dy));
            double nx = dx / distance, ny = dy / distance;
            double pull = ConnateTuning.MergeBondStrength * (0.28 + bond.Progress * 0.72) * dt;
            a.VelocityX += nx * pull; a.VelocityY += ny * pull;
            b.VelocityX -= nx * pull; b.VelocityY -= ny * pull;

            double sharedX = (a.VelocityX + b.VelocityX) * 0.5;
            double sharedY = (a.VelocityY + b.VelocityY) * 0.5;
            a.VelocityX += (sharedX - a.VelocityX) * bondDamping;
            a.VelocityY += (sharedY - a.VelocityY) * bondDamping;
            b.VelocityX += (sharedX - b.VelocityX) * bondDamping;
            b.VelocityY += (sharedY - b.VelocityY) * bondDamping;
        }
    }

    private static void ResolveMerges(List<ConnateBody> bodies, List<ConnatePendingMerge> pending,
                                      List<ConnateMergeEvent> events, ref long nextBodyId,
                                      List<ConnateGarbageClear>? garbageClears)
    {
        // Re-arbitrate matured bonds by ID in case several finish on the same substep, so a body participates
        // in at most one merge even under hostile/restored edge cases.
        ConnatePendingMerge[] matured = pending.Where(bond => bond.Age >= bond.Duration)
            .OrderBy(bond => bond.AId).ThenBy(bond => bond.BId).ToArray();
        if (matured.Length == 0) return;

        var byId = bodies.ToDictionary(body => body.Id);
        var used = new HashSet<long>();
        var accepted = new List<(ConnateBody A, ConnateBody B, int ResultRank)>();

        foreach (ConnatePendingMerge bond in matured)
        {
            if (used.Contains(bond.AId) || used.Contains(bond.BId)) continue;
            if (!byId.TryGetValue(bond.AId, out ConnateBody? a) || !byId.TryGetValue(bond.BId, out ConnateBody? b))
                continue;
            int resultRank = ConnateRules.MergeResultRank(a.Rank, b.Rank);
            if (resultRank < 0) continue;
            used.Add(a.Id); used.Add(b.Id);
            accepted.Add((a, b, resultRank));
        }

        if (accepted.Count == 0) return;
        // Garbage clearing is sampled at the actual logical merge instant, not when the bond first formed.
        // A garbage body touching either consumed source is scrubbed; bombs and mere contact never remove it.
        ConnateBody[] clearedGarbage = bodies.Where(body => body.IsGarbage && accepted.Any(merge =>
            Touching(body, merge.A) || Touching(body, merge.B))).OrderBy(body => body.Id).ToArray();
        var clearedGarbageIds = clearedGarbage.Select(body => body.Id).ToHashSet();
        bodies.RemoveAll(body => used.Contains(body.Id) || clearedGarbageIds.Contains(body.Id));
        pending.RemoveAll(bond => used.Contains(bond.AId) || used.Contains(bond.BId)
            || clearedGarbageIds.Contains(bond.AId) || clearedGarbageIds.Contains(bond.BId));
        if (garbageClears is not null)
            foreach (ConnateBody garbage in clearedGarbage)
                garbageClears.Add(new ConnateGarbageClear(
                    garbage.X, garbage.Y, garbage.Radius, garbage.Id, 0, garbage.Rotation));

        foreach (var merge in accepted)
        {
            // Preserve center of mass and momentum using the same area-based mass model as collision response.
            // The tiny inward bias helps newborn results rejoin the heap rather than surf its outer edge.
            double massA = merge.A.Mass;
            double massB = merge.B.Mass;
            double mass = massA + massB;
            double x = (merge.A.X * massA + merge.B.X * massB) / mass;
            double y = (merge.A.Y * massA + merge.B.Y * massB) / mass;
            double vx = (merge.A.VelocityX * massA + merge.B.VelocityX * massB) / mass;
            double vy = (merge.A.VelocityY * massA + merge.B.VelocityY * massB) / mass;
            double radius = Math.Sqrt(x * x + y * y);
            if (radius > 1e-8)
            {
                double inward = 0.035;
                vx -= x / radius * inward;
                vy -= y / radius * inward;
            }

            long shotId = Math.Max(merge.A.ShotId, merge.B.ShotId);
            bodies.Add(new ConnateBody
            {
                Id = nextBodyId++, Rank = merge.ResultRank, X = x, Y = y,
                // Family inheritance is non-trivial: blended tiles exist and a 1+2 pair may cross families —
                // see ConnateRules.MergedHue.
                Hue = ConnateRules.MergedHue(merge.A.Rank, merge.A.Hue, merge.B.Rank, merge.B.Hue,
                                             merge.ResultRank),
                VelocityX = vx, VelocityY = vy,
                MergeLock = ConnateTuning.MergeLockSeconds,
                SizeArmed = merge.A.SizeArmed || merge.B.SizeArmed,
                ShotId = shotId,
                JellyTime = ConnateTuning.JellySeconds,
            });
            events.Add(new ConnateMergeEvent(x, y, merge.ResultRank, shotId));
        }
    }

    private static double Distance(ConnateBody a, ConnateBody b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return new Vec2(dx, dy).Length;
    }

    /// <summary>Builds a union of convex local-space pieces, so ordinary convex SAT can run against concave
    /// silhouettes.</summary>
    private static ColliderGeometry BuildCollider(ConnateBody body)
    {
        // Shapes, not circles, where the drawing has corners:
        //  • The 1 (a five-pointed star) collides as a pentagon inset onto the star's points — a literal star
        //    outline would catch neighbours by a spike and read as sticky, a circle under-claims the points.
        //  • Garbage collides as its own drawn lump, from the same twelve-point id hash the renderer uses.
        //  • The 2 stays a circle: a complete disc whose star-shaped hole is decorative (a 1 meeting a 2
        //    merges, it never nests).
        //
        // ⚠ Both polygon colliders read body.Rotation, so they turn with the platter and with a fired tile's
        // spin. This is only correct because they are built from the same angle the renderer draws with —
        // never let the two drift apart.
        if (body.IsGarbage)
            return new ColliderGeometry(false, body.Radius, [BuildGarbageLump(body)]);
        if (body.Rank == 0)
            return new ColliderGeometry(false, body.Radius, [BuildPentagon(body)]);
        return new ColliderGeometry(true, body.Radius, []);
    }

    /// <summary>The 1's pentagon. Corners align with the star's five points — the star is drawn point-up at
    /// −90° before rotation, and this uses the same convention — inset by
    /// <see cref="ConnateTuning.StarColliderInset"/> so the very tips stay outside it.</summary>
    private static ConvexPart BuildPentagon(ConnateBody body)
    {
        double r = body.Radius * ConnateTuning.StarColliderInset;
        var points = new (double X, double Y)[5];
        for (int i = 0; i < 5; i++)
        {
            double a = -Math.PI / 2 + body.Rotation + i * Math.PI * 2 / 5;
            points[i] = (Math.Cos(a) * r, Math.Sin(a) * r);
        }
        return new ConvexPart(points);
    }

    /// <summary>Garbage's own silhouette, from the same id hash and lump formula
    /// <c>ConnateRenderer.DrawGarbage</c> draws with. ⚠ If that formula changes, this must change with it.
    /// Treated as convex by the SAT path: the lump varies only between 0.83× and 1.05× of the radius, so the
    /// approximation costs at most a shallow dent.</summary>
    private static ConvexPart BuildGarbageLump(ConnateBody body)
    {
        const int points = 12;
        double radius = body.Radius;
        var vertices = new (double X, double Y)[points];
        for (int i = 0; i < points; i++)
        {
            double angle = i * Math.PI * 2 / points + body.Rotation;
            ulong hash = (ulong)(body.Id * 0x9E3779B1L + i * 0x45D9F3BL);
            double lump = 0.83 + (hash & 255) / 255.0 * 0.22;
            vertices[i] = (Math.Cos(angle) * radius * lump, Math.Sin(angle) * radius * lump);
        }
        return new ConvexPart(vertices);
    }

    private static bool TryContact(ConnateBody a, ConnateBody b, in ColliderGeometry colliderA,
                                   in ColliderGeometry colliderB, out Contact contact)
    {
        if (colliderA.IsCircle && colliderB.IsCircle)
            return CircleContact(a, b, colliderA.BroadRadius, colliderB.BroadRadius, out contact);

        Contact? best = null;
        if (colliderA.IsCircle)
        {
            foreach (ConvexPart part in colliderB.Parts)
                if (CirclePolygonContact(a.X, a.Y, colliderA.BroadRadius, b, part, a.Id, b.Id,
                    out Contact found)) KeepDeepest(ref best, found!);
        }
        else if (colliderB.IsCircle)
        {
            foreach (ConvexPart part in colliderA.Parts)
            {
                if (!CirclePolygonContact(b.X, b.Y, colliderB.BroadRadius, a, part, b.Id, a.Id,
                    out Contact found)) continue;
                // Helper normal points circle -> polygon (B -> A); pair response requires A -> B.
                KeepDeepest(ref best, new Contact(-found!.NormalX, -found.NormalY, found.Penetration));
            }
        }
        else
        {
            foreach (ConvexPart partA in colliderA.Parts)
            foreach (ConvexPart partB in colliderB.Parts)
                if (PolygonContact(a, partA, b, partB, out Contact found)) KeepDeepest(ref best, found!);
        }

        contact = best.GetValueOrDefault();
        return best is not null;
    }

    private static void KeepDeepest(ref Contact? best, Contact found)
    {
        // Concave colliders are unions: resolving the deepest overlapping part is stable under solver
        // iteration and avoids hiding a deep second-part penetration behind a shallow first-part contact.
        if (best is null || found.Penetration > best.Value.Penetration) best = found;
    }

    private static bool CircleContact(ConnateBody a, ConnateBody b, double radiusA, double radiusB,
                                      out Contact contact)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double distanceSquared = dx * dx + dy * dy;
        double sum = radiusA + radiusB;
        if (distanceSquared >= sum * sum) { contact = default; return false; }
        if (distanceSquared <= 1e-16)
        {
            double angle = DegenerateAngle(a.Id, b.Id);
            contact = new Contact(Math.Cos(angle), Math.Sin(angle), sum);
            return true;
        }
        double distance = Math.Sqrt(distanceSquared);
        contact = new Contact(dx / distance, dy / distance, sum - distance);
        return true;
    }

    private static bool PolygonContact(ConnateBody a, ConvexPart partA, ConnateBody b, ConvexPart partB,
                                       out Contact contact)
    {
        double minimumOverlap = double.PositiveInfinity, bestX = 0, bestY = 0;
        if (!TestPolygonAxes(partA, partA, a.X, a.Y, partB, b.X, b.Y,
                ref minimumOverlap, ref bestX, ref bestY)
            || !TestPolygonAxes(partB, partA, a.X, a.Y, partB, b.X, b.Y,
                ref minimumOverlap, ref bestX, ref bestY))
        {
            contact = default;
            return false;
        }
        OrientAxis(a.X, a.Y, b.X, b.Y, a.Id, b.Id, ref bestX, ref bestY);
        contact = new Contact(bestX, bestY, minimumOverlap);
        return true;
    }

    private static bool TestPolygonAxes(ConvexPart axesFrom, ConvexPart a, double ax, double ay,
                                        ConvexPart b, double bx, double by,
                                        ref double minimumOverlap, ref double bestX, ref double bestY)
    {
        for (int i = 0; i < axesFrom.Points.Length; i++)
        {
            var p = axesFrom.Points[i];
            var q = axesFrom.Points[(i + 1) % axesFrom.Points.Length];
            double edgeX = q.X - p.X, edgeY = q.Y - p.Y;
            double nx = -edgeY, ny = edgeX;
            double length = Math.Sqrt(nx * nx + ny * ny);
            if (length < 1e-10) continue;
            nx /= length; ny /= length;
            ProjectPolygon(a, ax, ay, nx, ny, out double minA, out double maxA);
            ProjectPolygon(b, bx, by, nx, ny, out double minB, out double maxB);
            double overlap = Math.Min(maxA, maxB) - Math.Max(minA, minB);
            if (overlap <= 0) return false;
            if (overlap < minimumOverlap)
            {
                minimumOverlap = overlap; bestX = nx; bestY = ny;
            }
        }
        return true;
    }

    private static bool CirclePolygonContact(double circleX, double circleY, double radius,
                                             ConnateBody polygonBody, ConvexPart polygon,
                                             long circleId, long polygonId, out Contact contact)
    {
        double minimumOverlap = double.PositiveInfinity, bestX = 0, bestY = 0;
        for (int i = 0; i < polygon.Points.Length; i++)
        {
            var p = polygon.Points[i];
            var q = polygon.Points[(i + 1) % polygon.Points.Length];
            if (!TestCircleAxis(q.Y - p.Y, -(q.X - p.X))) { contact = default; return false; }
        }

        // Polygon edge normals alone are insufficient for circle/vertex separation. Test the axis from the
        // circle center to its closest polygon vertex as the curved equivalent of the missing SAT axis.
        var closest = polygon.Points.MinBy(point =>
        {
            double dx = polygonBody.X + point.X - circleX;
            double dy = polygonBody.Y + point.Y - circleY;
            return dx * dx + dy * dy;
        });
        if (!TestCircleAxis(polygonBody.X + closest.X - circleX,
                            polygonBody.Y + closest.Y - circleY))
        {
            contact = default;
            return false;
        }

        OrientAxis(circleX, circleY, polygonBody.X, polygonBody.Y, circleId, polygonId, ref bestX, ref bestY);
        contact = new Contact(bestX, bestY, minimumOverlap);
        return true;

        bool TestCircleAxis(double axisX, double axisY)
        {
            double length = Math.Sqrt(axisX * axisX + axisY * axisY);
            if (length < 1e-10) return true;
            double nx = axisX / length, ny = axisY / length;
            double centerProjection = circleX * nx + circleY * ny;
            double minCircle = centerProjection - radius, maxCircle = centerProjection + radius;
            ProjectPolygon(polygon, polygonBody.X, polygonBody.Y, nx, ny,
                out double minPolygon, out double maxPolygon);
            double overlap = Math.Min(maxCircle, maxPolygon) - Math.Max(minCircle, minPolygon);
            if (overlap <= 0) return false;
            if (overlap < minimumOverlap)
            {
                minimumOverlap = overlap; bestX = nx; bestY = ny;
            }
            return true;
        }
    }

    private static void ProjectPolygon(ConvexPart polygon, double x, double y, double nx, double ny,
                                       out double minimum, out double maximum)
    {
        double offset = x * nx + y * ny;
        minimum = double.PositiveInfinity; maximum = double.NegativeInfinity;
        foreach (var point in polygon.Points)
        {
            double projection = point.X * nx + point.Y * ny + offset;
            minimum = Math.Min(minimum, projection);
            maximum = Math.Max(maximum, projection);
        }
    }

    private static void OrientAxis(double ax, double ay, double bx, double by, long aId, long bId,
                                   ref double nx, ref double ny)
    {
        double direction = (bx - ax) * nx + (by - ay) * ny;
        if (Math.Abs(direction) < 1e-10)
        {
            double fallback = DegenerateAngle(aId, bId);
            direction = Math.Cos(fallback) * nx + Math.Sin(fallback) * ny;
        }
        if (direction < 0) { nx = -nx; ny = -ny; }
    }

    private static bool Touching(ConnateBody a, ConnateBody b)
    {
        // Reuse the real sector collider so garbage sitting inside a 2's missing mouth does not clear falsely.
        // Inflate only the garbage circle by a few solver-slop units to bridge positional-correction seams.
        ColliderGeometry colliderA = BuildCollider(a), colliderB = BuildCollider(b);
        double tolerance = ConnateTuning.CollisionSlop * 3;
        if (a.IsGarbage) colliderA = colliderA with { BroadRadius = colliderA.BroadRadius + tolerance };
        if (b.IsGarbage) colliderB = colliderB with { BroadRadius = colliderB.BroadRadius + tolerance };
        return TryContact(a, b, colliderA, colliderB, out _);
    }

    private static double DegenerateAngle(long a, long b)
    {
        // Coincident centers have no geometric normal. Hashing stable IDs supplies one without random state,
        // which keeps repeated runs and snapshot continuations byte-for-byte deterministic.
        ulong hash = (ulong)a * 0x9E3779B97F4A7C15UL ^ (ulong)b * 0xD1B54A32D192ED03UL;
        return (hash & 0xFFFF) / 65536.0 * Math.PI * 2;
    }
}
