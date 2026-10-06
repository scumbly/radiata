namespace ControllerWheel;

/// <summary>A contact between a ball and something: the unit normal pointing from the surface into the ball,
/// and how far the ball has sunk in.
///
/// <para><paramref name="Engulfed"/> marks the paddle case where the paddle is standing ON the ball rather
/// than meeting it — a swing that swept onto it. The response treats that as a bat strike instead of a
/// mirror; see <c>PetalPop.PaddleResponse</c>.</para></summary>
public readonly record struct PetalPopContact(double NX, double NY, double Depth, bool Engulfed = false)
{
    public Vec2 Normal => new(NX, NY);
}

/// <summary>Petalpop's contact tests, all discrete overlap at the fixed step. At the shipped speeds a step
/// moves a ball a third of a paddle's thickness, so tunnelling is impossible; what can go wrong is choosing
/// the wrong face at a seam, which the previous-centre rule below settles.</summary>
public static class PetalPopCollision
{
    /// <summary>Ball vs the paddle slab on <paramref name="side"/> — bent by <paramref name="bow"/> exactly as it is
    /// drawn: the distance from the ball to the sampled spine (<see cref="PetalPopLayout.PaddleSpine"/>) against half-thickness plus ball radius, with
    /// the contact normal from the closest spine point — which is what makes a tip hit throw the ball back
    /// toward the paddle's middle. <paramref name="offset"/> is where along the paddle the ball struck, −1
    /// (the corner-A end) .. +1 (the corner-B end).</summary>
    public static bool Paddle(PetalPopLayout L, int side, double pos, Vec2 ball, double ballRadius,
                              out PetalPopContact contact, out double offset, double lunge = 0, double bow = 0)
    {
        double reach = PetalPopTuning.PaddleHalfThickness + ballRadius;
        // Cheap reject: farther from the rail than the tilt's swing and the lunge can carry the paddle. The
        // tilt puts one end into the field and the other toward the gutter, so the band is two-sided.
        var s = L.Side[side];
        double swing = L.PaddleSwing(side, pos);
        double depth = s.Depth(ball);
        offset = 0;
        if (depth > swing + Math.Max(0, lunge) + reach || depth < -swing - reach + Math.Min(0, lunge) - PetalPopLayout.PaddleBend(bow))
        { contact = default; return false; }
        double rel = s.ParameterOf(ball) - pos / L.ArcRadius;
        if (Math.Abs(rel) > L.PaddleHalfAngle + (reach * 1.5 + swing) / L.ArcRadius) { contact = default; return false; }

        int n = PetalPopLayout.PaddleSegments;
        double best = double.PositiveInfinity;
        Vec2 closest = default;
        double bestOffset = 0;
        var p0 = L.PaddleSpine(side, pos, -1, lunge, bow);
        for (int i = 0; i < n; i++)
        {
            double o1 = -1 + 2.0 * (i + 1) / n;
            var p1 = L.PaddleSpine(side, pos, o1, lunge, bow);
            var e = p1 - p0;
            double len2 = e.Dot(e);
            double t = len2 > 1e-18 ? Math.Clamp((ball - p0).Dot(e) / len2, 0, 1) : 0;
            var q = p0 + e * t;
            double d = (ball - q).Length;
            if (d < best) { best = d; closest = q; bestOffset = -1 + 2.0 * (i + t) / n; }
            p0 = p1;
        }
        offset = bestOffset;
        var face = L.PaddleFaceNormal(side, pos);
        var toBall = ball - closest;
        double along = toBall.Dot(face);
        double lateral = Math.Sqrt(Math.Max(0, best * best - along * along));

        // Engulfed: the paddle is standing where the ball is — a released slingshot that swept through it.
        // Carry the ball out to the paddle's front face and hand back the field-facing normal, so it leaves
        // toward the middle. ⚠ Never report the "away from the spine" normal here: it points at the gutter,
        // and mirroring about it threw the ball out of bounds through its own paddle.
        if (along < 0 && lateral < reach)
        {
            contact = new(face.X, face.Y, reach - along, Engulfed: true);
            return true;
        }

        if (best >= reach) { contact = default; return false; }
        var normal = best > 1e-9 ? toBall * (1.0 / best) : face;
        contact = new(normal.X, normal.Y, reach - best);
        return true;
    }

    /// <summary>Ball vs a convex polygon (a petal quad or the core). With the centre outside, the contact is
    /// the closest boundary point (a rounded corner deflection is the right feel for a ball clipping a petal's
    /// edge). With the centre inside — which the shipped speeds never produce, but a hostile snapshot could —
    /// the face the ball came through (largest signed distance at its previous centre) is the exit.</summary>
    public static bool Polygon(Vec2[] poly, Vec2 prev, Vec2 ball, double ballRadius,
                               out PetalPopContact contact)
    {
        int n = poly.Length;
        var centroid = PetalPopLayout.Centroid(poly);
        bool inside = true;
        double bestPrev = double.NegativeInfinity, bestInsideDepth = double.NegativeInfinity;
        Vec2 exitNormal = default;
        double closest = double.PositiveInfinity;
        Vec2 closestPoint = default, closestNormal = default;

        for (int i = 0; i < n; i++)
        {
            var e0 = poly[i]; var e1 = poly[(i + 1) % n];
            var edge = e1 - e0;
            double len = edge.Length;
            if (len < 1e-12) continue;
            var dir = edge * (1.0 / len);
            var normal = new Vec2(dir.Y, -dir.X);
            if ((e0 - centroid).Dot(normal) < 0) normal = -normal;      // outward, whatever the winding

            double sd = (ball - e0).Dot(normal);
            if (sd > 0) inside = false;
            double sdPrev = (prev - e0).Dot(normal);
            // Candidate exit face: the one the previous centre was most clearly outside of.
            if (sdPrev > bestPrev) { bestPrev = sdPrev; exitNormal = normal; bestInsideDepth = sd; }

            double t = Math.Clamp((ball - e0).Dot(dir), 0, len);
            var q = e0 + dir * t;
            double dq = (ball - q).Length;
            if (dq < closest) { closest = dq; closestPoint = q; closestNormal = normal; }
        }

        if (inside)
        {
            contact = new(exitNormal.X, exitNormal.Y, ballRadius - bestInsideDepth);
            return true;
        }
        if (closest >= ballRadius) { contact = default; return false; }
        var cn = closest > 1e-9 ? (ball - closestPoint) * (1.0 / closest) : closestNormal;
        contact = new(cn.X, cn.Y, ballRadius - closest);
        return true;
    }

    /// <summary>Ball vs a circle (the gem).</summary>
    public static bool Circle(Vec2 centre, double radius, Vec2 ball, double ballRadius,
                              out PetalPopContact contact)
    {
        var d = ball - centre;
        double dist = d.Length, reach = radius + ballRadius;
        if (dist >= reach) { contact = default; return false; }
        var n = dist > 1e-9 ? d * (1.0 / dist) : new Vec2(0, -1);
        contact = new(n.X, n.Y, reach - dist);
        return true;
    }
}
