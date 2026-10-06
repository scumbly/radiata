namespace ControllerWheel;

/// <summary>
/// One physical body in the center heap. Numbered orbs and garbage deliberately share this type so gravity,
/// collision, blast, containment, and size-pressure behavior cannot drift apart. Garbage-specific rule gates
/// live in merge/scoring code and are identified by <see cref="IsGarbage"/>.
/// Coordinates use normalized screen-space field units: +X is right and +Y is down.
/// </summary>
public sealed class ConnateBody
{
    /// <summary>Monotonic deterministic identity used for ordering and degenerate collision normals.</summary>
    public long Id { get; init; }
    /// <summary>Arithmetic rank for numbered bodies. Ignored while <see cref="IsGarbage"/> is true.</summary>
    public int Rank { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }
    /// <summary>Seconds before this body may reserve another merge; primarily assigned to newborn results.</summary>
    public double MergeLock { get; set; }
    /// <summary>
    /// False while a new shot is travelling inward. Once armed, the body permanently participates in the
    /// clump-size envelope even if a later collision throws it outward.
    /// </summary>
    public bool SizeArmed { get; set; }
    /// <summary>Newest contributing fired-shot identity, propagated through merges to group visual cascades.</summary>
    public long ShotId { get; set; }
    public double Age { get; set; }
    /// <summary>Renderer-only time remaining for the damped post-merge jelly deformation.</summary>
    public double JellyTime { get; set; }
    /// <summary>Garbage has zero score/rank semantics and can only clear beside a resolving numbered merge.</summary>
    public bool IsGarbage { get; set; }
    /// <summary>Tile family (C1): <see cref="ConnateRules.HueAzure"/> or <see cref="ConnateRules.HueEmber"/>.
    /// Only same-family tiles merge. Garbage carries <see cref="ConnateRules.HueNone"/> — it has no arithmetic
    /// and merges with nothing.</summary>
    public int Hue { get; set; } = ConnateRules.HueNone;
    /// <summary>Current spin angle, radians. It also rotates star and garbage polygon colliders and is
    /// integrated in the physics step rather than the renderer so a frozen tile resumes at the same angle
    /// instead of snapping.</summary>
    public double Rotation { get; set; }
    /// <summary>Spin rate, radians/second, damped toward zero. Given only to a fired 1 or 2, whose
    /// wedge/mouth silhouette is the only place rotation is visible.</summary>
    public double SpinRate { get; set; }
    /// <summary>Allows a newly spawned garbage body to cross inward through the otherwise absolute outer wall.</summary>
    public bool EnteringPlayfield { get; set; }
    /// <summary>
    /// Per-body garbage collider radius chosen at spawn. Zero is accepted only for legacy/default construction
    /// and falls back to <see cref="ConnateTuning.GarbageRadius"/>.
    /// </summary>
    public double GarbageRadius { get; set; }

    public long Value => IsGarbage ? 0 : ConnateRules.ValueForRank(Rank);
    public double Radius => IsGarbage
        ? Math.Max(ConnateTuning.GarbageRadius, GarbageRadius)
        : ConnateTuning.RadiusForRank(Rank);
    /// <summary>Area multiplier used by collision impulse and merge centre-of-mass.
    ///
    /// <para>The 1 and the 2 are a third and two thirds of a 3's worth of matter, but both are drawn and
    /// collided at <see cref="ConnateTuning.StarPairFraction"/> of the 3's radius — a smaller circle than
    /// either share would fill. So both fractions divide that shrink back out, which is what keeps
    /// <c>mass(1) + mass(2) == mass(3)</c> exact: the invariant that makes a merge conserve momentum instead
    /// of nudging the heap.</para>
    ///
    /// <para>⚠ Both are derived, neither is a literal. A fraction above 1 is not a bug — it says the piece
    /// packs its share of a 3 into a footprint too small to hold it at unit density. Writing either as a
    /// number breaks mass conservation the moment the footprint is retuned, silently.</para></summary>
    public double AreaFraction => IsGarbage ? 1.0
        : Rank is 0 or 1
            ? (Rank + 1) / (3.0 * ConnateTuning.StarPairFraction * ConnateTuning.StarPairFraction)
            : 1.0;
    public double Mass => Radius * Radius * AreaFraction;
    public double RadiusFromCenter => Math.Sqrt(X * X + Y * Y);
}

// Immutable event records keep renderer timing out of the deterministic collision objects.
public readonly record struct ConnateMergeFlash(double X, double Y, int Rank, int ChainDepth, double Age);

public sealed record ConnateMergeEvent(double X, double Y, int ResultRank, long ShotId);

/// <summary>Bombs are swept projectiles, not heap bodies: they ignore garbage and destroy one numbered body.</summary>
public sealed class ConnateBombProjectile
{
    public long Id { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }
    public double Age { get; set; }
    public double Radius => ConnateTuning.BombRadius;
}

/// <summary>Short-lived presentation event; destroyed value is banked separately in the simulation.</summary>
public readonly record struct ConnateBombExplosion(double X, double Y, int DestroyedRank, double Age);
/// <summary>Presentation event emitted when a garbage body leaves the board. <paramref name="Rotation"/> is the
/// lump's turn at that instant, so the shatter breaks the art exactly as it was drawn.</summary>
public readonly record struct ConnateGarbageClear(double X, double Y, double Radius, long GarbageId, double Age, double Rotation = 0);

/// <summary>One combo, as a presentation event. Carries where it happened, how deep the combo was, and
/// <paramref name="PipIndex"/> — which bomb-charge pip it filled, so the renderer can fly a spark from the
/// merge to that exact pip and make the connection between "I chained" and "the meter moved" impossible to
/// miss.</summary>
public readonly record struct ConnateComboBurst(double X, double Y, int Count, int PipIndex, bool Completed, double Age);

/// <summary>A nugget of banked value flying from a destroyed tile to the score counter. The renderer draws
/// the arc; the sim owns the clock so the count-up and the arrival stay welded together.
///
/// <para><paramref name="Value"/> is this mote's share of the destroyed tile, and it is credited when the
/// mote lands — that arrival is the only way Connate scores at all.</para>
///
/// <para><paramref name="Delay"/> is how long it scatters outward before the counter starts pulling it in, so
/// a big tile's motes arrive as a stream rather than a lump: the count-up then ticks repeatedly instead of
/// jumping once, which is the whole point of paying in motes. Arrival is therefore at
/// <c>Delay + ScoreMoteFlightSeconds</c>, never at the flight time alone.</para>
///
/// <para><paramref name="Size"/> is a multiplier on the drawn nugget, and value is shared out in proportion to
/// it — the biggest nugget carries the biggest tick. <paramref name="Seed"/> gives the renderer stable
/// per-mote tumble/facet variation without a random draw, which would perturb the feed.</para></summary>
public readonly record struct ConnateScoreMote(double X, double Y, double DriftX, double DriftY, double Age, long Value,
                                      double Size = 1, double Delay = 0, long Seed = 0);

/// <summary>A reserved compatible pair visibly gathering itself before the actual merge.</summary>
public sealed class ConnatePendingMerge
{
    // IDs rather than object references make reservation state serializable and safe across list replacement.
    public long AId { get; init; }
    public long BId { get; init; }
    public int ResultRank { get; init; }
    public double Age { get; set; }
    public double Duration { get; init; }
    public double Progress => Math.Clamp(Age / Math.Max(0.01, Duration), 0, 1);
}
