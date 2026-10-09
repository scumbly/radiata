namespace ControllerWheel;

/// <summary>The board-clear encounter's stages, in order. <see cref="None"/> is "no encounter".</summary>
public enum ConnateBossPhase { None = 0, Drop = 1, Land = 2, Fight = 3, Break = 4 }

/// <summary>A bomb that reached the block: which bomb, and the point on its path nearest the block's centre.</summary>
public readonly record struct ConnateBossHit(long BombId, double X, double Y);

/// <summary>What <see cref="ConnateBossBlock.Step"/> asks <see cref="Connate"/> to do, because it owns the rail,
/// the bomb delivery and the board.</summary>
[Flags]
public enum ConnateBossEvent
{
    None = 0,
    /// <summary>The landing ring reached the loaded piece: shatter it and bring the first bomb in.</summary>
    ShatterHeld = 1,
    /// <summary>The fragments have left: restart ordinary play.</summary>
    Ended = 2,
}

/// <summary>The rail and delivery facts the block's phase machine reads and may not change.</summary>
/// <param name="HeldRadius">Radius of the piece on the rail, which the landing ring must reach.</param>
/// <param name="HeldIsBomb">Whether the rail holds a bomb (loaded, delivery finished or not).</param>
/// <param name="DeliveryInFlight">A bomb is still flying in from the meter.</param>
/// <param name="RailBombs">Bombs on the rail plus bombs still waiting for it.</param>
public readonly record struct ConnateBossContext(double HeldRadius, bool HeldIsBomb, bool DeliveryInFlight, int RailBombs);

/// <summary>The boss-block encounter that a cleared board starts: a crystal-crusted junk block drops in, its
/// landing ring shatters the loaded piece into a bomb, one bomb per hit cracks it, and the last breaks it.
///
/// <para>This class is the phase machine and the block's own motion. Everything that touches the board, the
/// rail, the score or the cues stays in <see cref="Connate"/>, which reads the
/// <see cref="ConnateBossEvent"/>s and the hit list this returns. It draws no random numbers.</para>
///
/// <para>⚠ The tether limit keeps the block's centre within <see cref="ConnateTuning.BossTetherLimit"/> of its
/// radius from the field centre, and a hit reaches <see cref="ConnateTuning.BossHitReach"/> of it past that, so
/// a shot aimed anywhere on the rim — which always passes through the field centre — must connect.</para></summary>
public sealed class ConnateBossBlock
{
    public ConnateBossPhase Phase { get; private set; }
    public bool Active => Phase != ConnateBossPhase.None;

    /// <summary>Seconds in the current phase.</summary>
    public double Time { get; private set; }
    /// <summary>What each hit pays, in banked value.</summary>
    public long Reward { get; private set; }
    /// <summary>Bombs this encounter takes to break the block: the rail's bombs plus the waiting ones.</summary>
    public int Bombs { get; private set; }
    public int Hits { get; private set; }
    /// <summary>One crack per hit, so the cracks drawn are simply the hits taken.</summary>
    public int Cracks => Hits;
    /// <summary>The stage index the whole encounter plays at; earned stages wait until it ends.</summary>
    public int CapturedStage { get; private set; }
    /// <summary>Whether the landing ring has already shattered the loaded piece.</summary>
    public bool Shattered { get; private set; }

    /// <summary>The block's centre, offset from the field centre by the spring tether.</summary>
    public double X { get; private set; }
    public double Y { get; private set; }
    public double VelocityX { get; private set; }
    public double VelocityY { get; private set; }
    /// <summary>The block's turn, radians. It starts turning when it lands.</summary>
    public double Rotation { get; private set; }
    /// <summary>Seconds of shake and impact flash left, started by the landing
    /// (<see cref="ConnateTuning.BossLandShakeSeconds"/>) and by each hit (<see cref="ConnateTuning.BossHitFlashSeconds"/>).</summary>
    public double ImpactLeft { get; private set; }

    public double Radius => RestingRadius(Bombs);

    /// <summary>The block's size in the current phase: shrinking from the start radius to its own while it
    /// falls, its own after.</summary>
    public double DrawRadius
    {
        get
        {
            if (Phase != ConnateBossPhase.Drop) return Radius;
            double ease = ArcadeMath.Smoothstep(DropProgress);
            return ConnateTuning.BossDropStartRadius + (Radius - ConnateTuning.BossDropStartRadius) * ease;
        }
    }

    /// <summary>0..1 across the drop; 1 once the block has landed.</summary>
    public double DropProgress => Phase == ConnateBossPhase.Drop
        ? Math.Clamp(Time / Math.Max(0.05, ConnateTuning.BossDropSeconds), 0, 1)
        : Active ? 1 : 0;

    /// <summary>The block's opacity while it falls in; opaque from the time it is a third of the way down.</summary>
    public double Opacity => Phase == ConnateBossPhase.Drop ? Math.Clamp(DropProgress * 1.8, 0, 1) : 1;

    /// <summary>The landing ring's radius, or 0 outside the landing phase.</summary>
    public double WaveRadius => Phase == ConnateBossPhase.Land
        ? Radius + Time * ConnateTuning.BossWaveSpeed : 0;

    /// <summary>Resting radius for an encounter of <paramref name="bombs"/> bombs.</summary>
    public static double RestingRadius(int bombs) =>
        ConnateTuning.BossRadiusBase + ConnateTuning.BossRadiusPerBomb * bombs;

    /// <summary>What each hit pays: the bonus is half the bank (rounded to the nearest integer), capped at the
    /// whole gap between the level the board was cleared at and the next; a hit pays half of that, rounded down. Level 20 has no next level and uses the gap into it (level 19 → 20). Integers
    /// throughout, so the pay-out re-adds exactly.</summary>
    /// <param name="banked">The banked total at the clear, motes still in flight included.</param>
    /// <param name="stageIndex">The stage index at the clear, 0..19.</param>
    public static long RewardFor(long banked, int stageIndex)
    {
        long bonus = banked <= 0 ? 0 : (long)Math.Round(banked * ConnateTuning.BossBonusShare);
        int level = Math.Clamp(stageIndex, 0, ConnateTuning.MaximumStageIndex) + 1;
        if (level > ConnateTuning.MaximumStageIndex) level--;
        long gap = ConnateTuning.StageThresholdFor(level + 1) - ConnateTuning.StageThresholdFor(level);
        return Math.Min(bonus, gap) / 2;
    }

    public void Begin(long reward, int bombs, int stageIndex)
    {
        Phase = ConnateBossPhase.Drop;
        Time = 0;
        Reward = reward;
        Bombs = bombs;
        Hits = 0;
        CapturedStage = stageIndex;
        Shattered = false;
        X = Y = VelocityX = VelocityY = 0;
        Rotation = 0;
        ImpactLeft = 0;
    }

    public void Clear()
    {
        Phase = ConnateBossPhase.None;
        Time = 0;
        Reward = 0;
        Bombs = Hits = CapturedStage = 0;
        Shattered = false;
        X = Y = VelocityX = VelocityY = 0;
        Rotation = 0;
        ImpactLeft = 0;
    }

    /// <summary>Resumes a saved encounter. The caller has already rejected anything non-finite or out of range;
    /// this still clamps the tether so a stored displacement cannot start outside its limit.</summary>
    public void Load(ConnateBossPhase phase, double time, long reward, int bombs, int hits, int stageIndex,
                     bool shattered, double x, double y, double vx, double vy, double rotation, double impactLeft)
    {
        Phase = phase;
        Time = time;
        Reward = reward;
        Bombs = bombs;
        Hits = hits;
        CapturedStage = stageIndex;
        Shattered = shattered;
        X = x; Y = y; VelocityX = vx; VelocityY = vy;
        ClampTether();
        Rotation = ArcadeMath.NormTau(rotation);
        ImpactLeft = impactLeft;
    }

    /// <summary>Advances the encounter by <paramref name="dt"/>. In the fight it also moves
    /// <paramref name="bombs"/> and removes the ones that hit, appending a <see cref="ConnateBossHit"/> for each
    /// to <paramref name="hits"/>; the caller owns that list so a step allocates nothing.</summary>
    public ConnateBossEvent Step(double dt, in ConnateBossContext ctx, List<ConnateBombProjectile> bombs,
                                 List<ConnateBossHit> hits)
    {
        var result = ConnateBossEvent.None;
        if (!Active) return result;
        Time += dt;
        ImpactLeft = Math.Max(0, ImpactLeft - dt);
        if (Phase != ConnateBossPhase.Drop)
            Rotation = (Rotation + ConnateTuning.BossSpinPerSec * dt) % (Math.PI * 2);

        switch (Phase)
        {
            case ConnateBossPhase.Drop:
                if (Time >= ConnateTuning.BossDropSeconds)
                {
                    Phase = ConnateBossPhase.Land;
                    Time = 0;
                    ImpactLeft = ConnateTuning.BossLandShakeSeconds;
                }
                break;

            case ConnateBossPhase.Land:
                bool justShattered = false;
                if (!Shattered && WaveRadius >= ConnateTuning.LaunchRadius - ctx.HeldRadius)
                {
                    Shattered = justShattered = true;
                    result |= ConnateBossEvent.ShatterHeld;
                }
                // The fight opens only on a bomb the player can fire: shattered, read, and delivered.
                if (Shattered && !justShattered && Time >= ConnateTuning.BossFightDelaySeconds
                    && !ctx.DeliveryInFlight && ctx.HeldIsBomb)
                {
                    Phase = ConnateBossPhase.Fight;
                    Time = 0;
                }
                break;

            case ConnateBossPhase.Fight:
                StepFight(dt, ctx, bombs, hits);
                break;

            case ConnateBossPhase.Break:
                if (Time >= ConnateTuning.BossBreakSeconds)
                {
                    Phase = ConnateBossPhase.None;
                    result |= ConnateBossEvent.Ended;
                }
                break;
        }
        return result;
    }

    private void StepFight(double dt, in ConnateBossContext ctx, List<ConnateBombProjectile> bombs,
                           List<ConnateBossHit> hits)
    {
        // A damped spring to the centre. Its displacement is bounded inside the block's own core, so every
        // inward trajectory still reaches it.
        VelocityX += (-ConnateTuning.BossTetherStiffness * X - ConnateTuning.BossTetherDamping * VelocityX) * dt;
        VelocityY += (-ConnateTuning.BossTetherStiffness * Y - ConnateTuning.BossTetherDamping * VelocityY) * dt;
        X += VelocityX * dt;
        Y += VelocityY * dt;
        ClampTether();

        for (int i = 0; i < bombs.Count;)
        {
            ConnateBombProjectile bomb = bombs[i];
            double dx = bomb.VelocityX * dt, dy = bomb.VelocityY * dt;
            double lengthSquared = dx * dx + dy * dy;
            double along = lengthSquared > 0
                ? Math.Clamp(((X - bomb.X) * dx + (Y - bomb.Y) * dy) / lengthSquared, 0, 1) : 0;
            double nearX = bomb.X + dx * along, nearY = bomb.Y + dy * along;
            double reach = Radius * ConnateTuning.BossHitReach + bomb.Radius;
            double gx = nearX - X, gy = nearY - Y;
            if (gx * gx + gy * gy <= reach * reach)
            {
                hits.Add(new ConnateBossHit(bomb.Id, nearX, nearY));
                Hits++;
                ImpactLeft = ConnateTuning.BossHitFlashSeconds;
                VelocityX += bomb.VelocityX * ConnateTuning.BossHitKick;
                VelocityY += bomb.VelocityY * ConnateTuning.BossHitKick;
                Rotation = (Rotation + ConnateTuning.BossHitSpin) % (Math.PI * 2);
                bombs.RemoveAt(i);
                if (Hits >= Bombs)
                {
                    Phase = ConnateBossPhase.Break;
                    Time = 0;
                }
                continue;
            }
            bomb.X += dx;
            bomb.Y += dy;
            bomb.Age += dt;
            if (bomb.Age >= ConnateTuning.BombLifetimeSeconds
                || Math.Sqrt(bomb.X * bomb.X + bomb.Y * bomb.Y) > ConnateTuning.OuterHardLimitRadius + 0.1)
                bombs.RemoveAt(i);
            else i++;
        }

        // Unreachable from legal play (every bomb connects), but a restored board must never wait for a bomb
        // that does not exist: with nothing in flight, on the rail or waiting, the block simply breaks.
        if (Phase == ConnateBossPhase.Fight && bombs.Count == 0 && ctx.RailBombs <= 0)
        {
            Phase = ConnateBossPhase.Break;
            Time = 0;
        }
    }

    private void ClampTether()
    {
        double limit = Radius * ConnateTuning.BossTetherLimit;
        double distance = Math.Sqrt(X * X + Y * Y);
        if (distance > limit && distance > 0) { X *= limit / distance; Y *= limit / distance; }
    }
}
