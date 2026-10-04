using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>
/// Stateless, code-only WPF renderer for Connate. It converts normalized simulation coordinates to pixels and
/// derives all animation from simulation-owned ages/timers, so drawing never changes game outcome.
/// </summary>
internal sealed class ConnateRenderer : IArcadeRenderer
{
    // Renderer-only framing scale: a uniform mapping that puts the craft center at 93.6% of the visible radius
    // and enlarges positions, bodies, rule rings, trails, and effects together. ⚠ Never use this constant from
    // Core — changing it reframes the same gameplay state without altering collision radii, launch timing,
    // clump limits, gravity, or saved snapshots.
    private const double WorldToFieldScale = 1.04;

    /// <summary>The ground art's half-width against the field radius. The art is authored to the rule
    /// geometry — its well edge and its ring sit at the limit and cushion radii — with a margin past the
    /// field for bleed, so the picture is drawn this much larger than the field and clipped to it. ⚠ Fitted
    /// to the shipped connate-background file (well edge 188 px and ring 269 px on a 348 px half-width);
    /// re-fit if that file is re-exported at a different framing.</summary>
    private const double GroundArtScale = 1.0623;

    /// <summary>A black wash over the ground art, so the supplied file is darkened on screen without being
    /// re-exported. Alpha 26 of 255 is 10%.</summary>
    private static readonly Brush GroundArtWash = Frozen(new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)));
    private static Brush Frozen(Brush b) { b.Freeze(); return b; }

    /// <summary>How far past a piece's radius its shadow reaches, and how far out the shadow stays at full
    /// strength before feathering to nothing. Together they set the fuzz.</summary>
    private const double PieceShadowReach = 1.30;
    private const double PieceShadowCore = 0.60;
    /// <summary>One radial brush for every shadow, in relative coordinates so it fits any radius: solid to
    /// the core stop, then feathered to the edge.</summary>
    private static readonly Brush PieceShadow = Frozen(new RadialGradientBrush(
    [
        new GradientStop(Color.FromArgb(120, 0, 0, 0), 0),
        new GradientStop(Color.FromArgb(120, 0, 0, 0), PieceShadowCore),
        new GradientStop(Color.FromArgb(0, 0, 0, 0), 1),
    ]));

    /// <summary>A soft, centred shadow under a piece of the given radius. The 1 is a star, so its shadow is
    /// the same star at the shadow's reach, turned with the piece; the radial brush feathers it toward the
    /// points.</summary>
    private static void DrawPieceShadow(DrawingContext dc, Point p, double radius, bool star = false,
                                        double rotation = 0)
    {
        double r = radius * PieceShadowReach;
        if (r < 1) return;
        if (!star) { dc.DrawEllipse(PieceShadow, null, p, r, r); return; }
        dc.PushTransform(new RotateTransform(rotation * 180 / Math.PI, p.X, p.Y));
        dc.DrawGeometry(PieceShadow, null, StarGeometry(p, r, ConnateTuning.StarInnerFraction));
        dc.Pop();
    }

    // The field-disc clip for the ground art, rebuilt only when the field moves or resizes.
    private static EllipseGeometry? _fieldClip;
    private static Point _fieldClipCenter;
    private static double _fieldClipRadius;
    private static Geometry FieldClip(Point c, double radius)
    {
        if (_fieldClip is null || _fieldClipCenter != c || _fieldClipRadius != radius)
        {
            _fieldClip = new EllipseGeometry(c, radius, radius);
            _fieldClip.Freeze();
            _fieldClipCenter = c; _fieldClipRadius = radius;
        }
        return _fieldClip;
    }

    public static readonly ConnateRenderer Instance = new();
    public Brush Accent => ConnatePalette.Aim;
    public bool CoversField => true;   // the opaque Outfield below paints the whole field

    public void Draw(DrawingContext dc, Point c, double fieldRadius, IArcadeGame game, double ppd)
    {
        if (game is not Connate connate) return;
        // One shared scale preserves exact visual proportions between balls, boundaries, trajectories, and effects.
        // Keeping this multiplication centralized prevents a future maintainer from enlarging only art or only rules.
        double world = fieldRadius * WorldToFieldScale;
        double baseTileRadius = ConnateTuning.TileRadius * world;

        // ⚠ Back-to-front order is load-bearing: static field, limit/aim guides, trails/bonds, physical bodies,
        // event effects, player payload, then modal state overlays.
        // ── The outfield is dark ──
        // The whole field is the dark outfield and the safe zone is painted back in as the one bright disc, so
        // the boundary is a change in ground rather than a line drawn on it. Don't invert this: unsafe ground
        // must never read as more open than safe ground.
        double limit = world * ConnateTuning.ClumpLimitRadius;
        // Supplied ground art stands in for the vector fills and gravity rings below it; the rule rings and
        // every dynamic overlay are still drawn over it, since they carry the game's state.
        var ground = ArcadeSprites.Get(ArcadeSprites.Slot.ConnateBackground);
        bool groundArt = ground is not null;
        if (ground is not null)
        {
            dc.PushClip(FieldClip(c, fieldRadius));
            ArcadeSprites.Draw(dc, ground, ArcadeSprites.Box(c, fieldRadius * GroundArtScale), ArcadeSprites.Fit.Cover);
            dc.DrawEllipse(GroundArtWash, null, c, fieldRadius, fieldRadius);
            dc.Pop();
        }
        else
        {
            dc.DrawEllipse(ConnatePalette.Outfield, null, c, fieldRadius, fieldRadius);
            dc.DrawEllipse(ConnatePalette.RimFall(Math.Min(0.92, limit / fieldRadius)), null,
                c, fieldRadius, fieldRadius);
            dc.DrawEllipse(ConnatePalette.Well, null, c, limit, limit);
            dc.DrawEllipse(ConnatePalette.GravityGlow, null, c, fieldRadius * 0.44, fieldRadius * 0.44);
            for (int i = 1; i <= 4; i++)
            {
                double radius = limit * i / 5.0;
                dc.DrawEllipse(null, ConnatePalette.GravityPen, c, radius, radius);
            }
        }

        DrawDoomFlood(dc, c, limit, fieldRadius, connate.SizeFuse);
        // The ground art carries the limit edge and the cushion ring itself, at the same radii; stroking them
        // again over it only thickens its lines.
        double cushion = world * ConnateTuning.OuterCushionStartRadius;
        if (!groundArt)
        {
            dc.DrawEllipse(null, ConnatePalette.LimitPen, c, limit, limit);
            dc.DrawEllipse(null, ConnatePalette.AimStroke(255, Math.Max(1, fieldRadius * 0.004)),
                c, cushion, cushion);
        }
        // Bomb immunity replaces the danger fuse with a calm breathing shield on the same readable rule ring.
        if (connate.SizeLimitImmune)
        {
            double pulse = 1 + Math.Sin(connate.PhaseTime * Math.PI * 4) * 0.012;
            dc.DrawEllipse(null, ConnatePalette.AimStroke(255, Math.Max(2, fieldRadius * 0.012)),
                c, limit * pulse, limit * pulse);
        }
        else DrawFuse(dc, c, limit, connate.SizeFuse, fieldRadius);
        DrawAim(dc, c, world, connate);

        // Pending-bond deformation is accumulated by body ID, leaving logical circle collision shapes untouched.
        // A plain loop, not ToDictionary: a duplicate id (a snapshot from another schema) must not throw out
        // of the render pass. Last one wins, which is what the sim's own lookup would see.
        var byId = new Dictionary<long, ConnateBody>(connate.Bodies.Count);
        foreach (ConnateBody b in connate.Bodies) byId[b.Id] = b;
        var bondShape = new Dictionary<long, (double Squash, double Rotation)>();
        // How far each bonded tile leans toward its partner, and who that partner is — the lean has to be
        // resolved against live positions at draw time, so the direction can't be baked in here.
        var bondLean = new Dictionary<long, double>();
        var bondPartner = new Dictionary<long, long>();
        foreach (ConnateBody body in connate.Bodies)
            DrawMotionTrail(dc, c, world, body);

        foreach (ConnatePendingMerge bond in connate.PendingMerges)
        {
            if (!byId.TryGetValue(bond.AId, out ConnateBody? a)
                || !byId.TryGetValue(bond.BId, out ConnateBody? b)) continue;
            DrawBond(dc, c, world, a, b, bond, connate.PhaseTime);
            double angle = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;

            // ── Reaching for each other like a magnetic blob ──
            // ⚠ The sign must stay positive. `rotation` aligns the tile's local X with the axis between the
            // pair and DrawOrb reads `squash` as X-scale, so a negative value pinches the tiles along that
            // axis and bulges them across it — pulling them apart instead of together.
            //
            // Ramped rather than sine-humped: a sine peaks mid-bond and is back to nothing at the moment of
            // the merge, i.e. gone exactly when the stretch should be greatest. This grows to its maximum
            // right as they combine, and the jelly on the newborn takes over from there.
            double reach = ConnateTuning.AnticipationStretch * EaseIn(bond.Progress);
            bondShape[a.Id] = (reach, angle);
            bondShape[b.Id] = (reach, angle);

            // …and each tile leans bodily toward the other. The stretch alone reads as two tiles inflating;
            // it's the closing gap that makes it read as attraction. Purely visual — physics never sees it.
            double lean = ConnateTuning.AnticipationLean * EaseIn(bond.Progress);
            bondLean[a.Id] = lean;
            bondLean[b.Id] = lean;
            bondPartner[a.Id] = b.Id;
            bondPartner[b.Id] = a.Id;
        }

        // ── Name the tiles that are ending the run ──
        // ClumpExtent is a max over armed bodies, so a single stray tile can be the entire reason the fuse is
        // burning while the rest of the heap sits comfortably inside. The fuse ring says "something is out";
        // this says which, so the answer is a shot you can aim rather than a guess. The pulse quickens with
        // the fuse, so urgency is legible without reading the ring at all.
        //
        // ⚠ The phase is integrated in the sim (Connate.OverLimitPhase); never compute it here as
        // PhaseTime × rate. Scaling an accumulating clock by a rate that varies with the fuse adds a
        // 2·t·fuse′ term to the real frequency, which makes the pulse faster the longer the board has been
        // alive and makes the tuning constant do nothing visible. See the remark on OverLimitPhase.
        double overPulse = 0.5 + 0.5 * Math.Sin(connate.OverLimitPhase);
        bool Overboard(ConnateBody b) =>
            b.SizeArmed && b.RadiusFromCenter + b.Radius > ConnateTuning.ClumpLimitRadius;

        // A blended tile's shine: 0..1 while crossing, −1 the rest of the time. Staggered per body so a
        // board of them ripples rather than flashing in unison.
        double BlendSweep(ConnateBody b)
        {
            if (b.Hue != ConnateRules.HueBlend) return -1;
            double period = Math.Max(0.2, ConnateTuning.BlendSweepPeriodSeconds);
            double phase = (connate.PhaseTime + b.Id * ConnateTuning.BlendSweepStagger) % period;
            return phase < ConnateTuning.BlendSweepSeconds
                ? phase / Math.Max(0.05, ConnateTuning.BlendSweepSeconds) : -1;
        }

        // The merge burst sits under the heap: the ring and its sparks are a glow the new tile rises out of,
        // not a mark drawn over it, so the result tile stays fully readable at the instant it appears.
        foreach (ConnateMergeFlash flash in connate.MergeFlashes)
        {
            double progress = Math.Clamp(flash.Age / ConnateTuning.MergeFlashSeconds, 0, 1);
            double flashTileRadius = ConnateTuning.RadiusForRank(flash.Rank) * world;
            double radius = flashTileRadius * (0.8 + progress * (1.2 + Math.Min(3, flash.ChainDepth) * 0.18));
            // A glow under the heap, not a highlight over it: up from nothing in the first sixth of the
            // flash, then down to nothing, peaking at half strength.
            double envelope = progress < MergeFlashAttack ? progress / MergeFlashAttack
                                                          : (1 - progress) / (1 - MergeFlashAttack);
            byte alpha = (byte)(110 * Math.Clamp(envelope, 0, 1));
            Pen pen = ConnatePalette.WhitePen(alpha, Math.Max(1, flashTileRadius * 0.05));
            Point flashCenter = Screen(c, world, flash.X, flash.Y);
            dc.DrawEllipse(null, pen, flashCenter, radius, radius);
            DrawMergeSparks(dc, flashCenter, flashTileRadius, flash, progress, pen, alpha);
        }
        // ── Shadows, as a pass of their own under the whole heap ──
        // Every piece on the board casts the same soft halo straight down (no offset): drawn before any piece,
        // so a shadow never lies across a neighbour, and as one ellipse each off a single frozen radial brush.
        foreach (ConnateBody body in connate.Bodies)
            DrawPieceShadow(dc, Screen(c, world, body.X, body.Y), body.Radius * world,
                            star: !body.IsGarbage && body.Rank == 0, body.Rotation);
        foreach (ConnateBombProjectile bomb in connate.BombProjectiles)
            DrawPieceShadow(dc, Screen(c, world, bomb.X, bomb.Y), bomb.Radius * world * BombArtScale);

        foreach (ConnateBody body in connate.Bodies.OrderBy(body => body.Id))
        {
            if (body.IsGarbage)
            {
                // ⚠ Garbage must turn with the platter: the seeded opening blob sits at dead centre, where a
                // rigid rotation about the origin moves it nowhere, so without its own spin it is the one
                // body that looks bolted to the screen.
                // Garbage counts toward the envelope like anything else, so it warns like anything else.
                DrawGarbage(dc, Screen(c, world, body.X, body.Y), body.Radius * world,
                    body.Id, connate.PhaseTime, body.Rotation,
                    Overboard(body) ? overPulse : -1);
                continue;
            }
            (double squash, double rotation) = bondShape.GetValueOrDefault(body.Id);
            // Jelly is an exponentially damped render transform only; physics continues using the true circle.
            if (body.JellyTime > 0)
            {
                double age = 1 - body.JellyTime / Math.Max(0.05, ConnateTuning.JellySeconds);
                double jelly = Math.Sin(age * Math.PI * 5.5) * Math.Exp(-age * 3.1) * 0.22;
                squash += jelly;
                rotation = ((body.Id & 1) == 0 ? -1 : 1) * 18 * Math.Sin(age * Math.PI * 3) * Math.Exp(-age * 3.4);
            }
            // A fired 1 or 2 spins. Rotation is sim state (ConnateBody.Rotation), not a render clock, so a
            // frozen tile resumes at the angle it stopped at instead of snapping.
            rotation += body.Rotation * 180 / Math.PI;

            // The lean toward a merge partner, applied to the drawn position only.
            double drawX = body.X, drawY = body.Y;
            if (bondLean.TryGetValue(body.Id, out double leanBy)
                && bondPartner.TryGetValue(body.Id, out long partnerId)
                && byId.TryGetValue(partnerId, out ConnateBody? partner))
            {
                drawX += (partner.X - body.X) * leanBy;
                drawY += (partner.Y - body.Y) * leanBy;
            }
            DrawOrb(dc, Screen(c, world, drawX, drawY), body.Radius * world, body.Rank, body.Hue, ppd,
                squash, rotation, -(connate.PhaseTime * 44 + body.Id * 37),
                Overboard(body) ? overPulse : -1, BlendSweep(body));
        }

        foreach (ConnateBombProjectile bomb in connate.BombProjectiles)
        {
            Point p = Screen(c, world, bomb.X, bomb.Y);
            double speed = Math.Sqrt(bomb.VelocityX * bomb.VelocityX + bomb.VelocityY * bomb.VelocityY);
            if (speed > 0.01)
            {
                Point tail = new(p.X - bomb.VelocityX / speed * world * 0.13,
                    p.Y - bomb.VelocityY / speed * world * 0.13);
                dc.DrawLine(ConnatePalette.AimStroke(255, Math.Max(2, bomb.Radius * world * 0.48)), tail, p);
            }
            DrawBomb(dc, p, bomb.Radius * world, connate.PhaseTime + bomb.Id * 0.17, art: true);
        }

        foreach (ConnateBombExplosion explosion in connate.BombExplosions)
            DrawBombExplosion(dc, c, world, explosion);

        foreach (ConnateGarbageClear clear in connate.GarbageClears)
            DrawGarbageClear(dc, c, world, clear);


        DrawScoreboard(dc, c, fieldRadius, connate, ppd);
        // Both of these land on the scoreboard, so both must draw over it: the nuggets on the number, the
        // combo sparks in the charge meter. A payout that disappears behind the thing it is paying is the one
        // frame of the effect that has to be visible.
        DrawScoreMotes(dc, c, world, fieldRadius, connate);
        DrawComboBursts(dc, c, world, fieldRadius, connate, ppd);

        // The craft is below its payload/clock so the loaded piece remains readable at couch distance.
        DrawCraft(dc, c, world, connate.PlayerAngle, baseTileRadius, connate.PhaseTime, connate.SinceFire,
                  connate.FireHeld, connate.SinceHold, connate.ShotDrawTension, connate.AutoWindCharge);
        // ⚠ Carries the craft's breath and its bow draw — both of them, for the same reason. The craft rocks
        // gently in and out at rest and draws back as ✕ is held; a payload sitting at a fixed radius drifted
        // out of its own grip, so every term that moves one has to ride both. The piece is nocked: it goes
        // back with the string and comes forward with it. The clamps and the shot clock are placed off this
        // point, so they follow on their own.
        Point held = Polar(c, world * (ConnateTuning.LaunchRadius * PayloadInset - PayloadForward
                                       + CraftBreath(connate.PhaseTime)
                                       + CraftDraw(connate.FireHeld, connate.SinceHold, connate.SinceFire,
                                                   connate.ShotDrawTension, connate.AutoWindCharge)),
                           connate.PlayerAngle);
        double heldPixelRadius = (connate.HeldIsBomb ? ConnateTuning.BombRadius
            : ConnateTuning.RadiusForRank(connate.HeldRank)) * world;
        // ⚠ Draw before the payload: the sweep is a disc under the tile, not a ring over it. Its radius scales
        // off the tile's own size, so a big tile still leaves a visible collar of clock around itself rather
        // than swallowing it.
        //
        // ShotClockUrgency, not ShotClockProgress: zero until the final third, then a full 0→1 across it.
        DrawShotClock(dc, held, heldPixelRadius * 1.34, connate.ShotClockUrgency, fieldRadius, connate.PhaseTime);

        // ⚠ The sim flips HeldIsBomb on frame 0 of the flight, so the payload slot is "a bomb" for the whole
        // delivery. Don't draw it there until the flight lands, or the bomb is in two places at once — sitting
        // ready and still arriving. The delivery draw below is the only bomb on screen until then.
        if (connate.HeldIsBomb)
        {
            if (!connate.BombDelivering)
            {
                // Turned with the craft like any other loaded piece, so the payload is never the one thing on
                // the rim still pinned to the screen's up.
                dc.PushTransform(new RotateTransform(CraftFacing(connate.PlayerAngle) + LoadedBombLean, held.X, held.Y));
                DrawBomb(dc, held, ConnateTuning.BombRadius * world, connate.PhaseTime, art: true);
                dc.Pop();
            }
        }
        // ⚠ The loaded piece turns with the craft, off the same facing the hull and the grips read: a piece
        // pinned upright with an independent swirl reads as idling in a holder rather than clamped into a
        // launcher that is itself turning. The blend's S-curve rides that facing too, for the same reason —
        // its own clock is the visible half of the drift.
        // The numeral is untouched: DrawOrb draws labels outside the rotation, so it stays upright wherever
        // the craft points.
        // ⚠ Squash stays zero: the loaded piece does not breathe, or it reads as alive rather than clamped
        // and fights the craft's own rock. The parameter stays because the heap still uses it for merge
        // anticipation and jelly.
        else DrawOrb(dc, held, ConnateTuning.RadiusForRank(connate.HeldRank) * world, connate.HeldRank,
            connate.HeldHue, ppd, 0, CraftFacing(connate.PlayerAngle),
            CraftFacing(connate.PlayerAngle));

        // Last over the payload, so the grip reads as being on top of whatever is loaded.
        // ⚠ The grips let go as the shot leaves, and are back on the next piece the moment the release
        // animation ends. Held or at rest they are shut; only the release window is clampless.
        if ((!connate.HeldIsBomb || !connate.BombDelivering)
            && connate.SinceFire >= ArcadeSprites.Slot.ConnateGripReleaseSeconds)
            DrawClamps(dc, held, heldPixelRadius, connate.PlayerAngle);

        // ⚠ After the clamps, so the shower is never buried under a grip. The sparks are drawn in screen
        // space (they fall down the screen), but their flame anchor turns with the casing.
        if (connate.HeldIsBomb && !connate.BombDelivering)
            DrawBombFuseSparks(dc, held, ConnateTuning.BombRadius * world, connate.PhaseTime,
                               CraftFacing(connate.PlayerAngle) + LoadedBombLean);

        // The bomb flying from the meter into the payload.
        DrawBombDelivery(dc, c, fieldRadius, connate, world, ppd);

        // Over everything, including the payload and the clock — a combo is the loudest thing that happens.
        DrawComboBanner(dc, c, fieldRadius, connate, ppd);
        // …and a board clear is louder still, so it goes last of all. The two can overlap for a moment (the
        // bomb that empties the board is usually also finishing a cascade) and they sit at different heights
        // on purpose, so neither has to be suppressed.
        DrawBoardClearBanner(dc, c, fieldRadius, connate, ppd);

        if (connate.Phase == Connate.Stage.Intro) DrawIntro(dc, c, fieldRadius, ppd);
        else if (connate.Phase == Connate.Stage.LimitBreach)
            dc.DrawEllipse(ConnatePalette.Danger, null, c, fieldRadius * 0.025, fieldRadius * 0.025);
        else if (connate.Phase == Connate.Stage.GameOver) DrawGameOver(dc, c, fieldRadius, connate, ppd);
    }

    /// <summary>The score digits' centre: the middle of the HUD plate that hangs in from the top of the disc.
    /// The plate is laid out around this point in DrawScoreboard, so everything else that aims at the score
    /// (combo sparks, the bomb delivery's source) keeps aiming here.
    ///
    /// <para>Where banked value lands too: at 12 o'clock, in the annulus between the clump limit
    /// (0.573 × field) and the craft's orbit (0.936 ×) — the only band of this playfield that nothing
    /// permanently occupies. Motes aim here, so it has to be one number both this and
    /// <see cref="DrawScoreMotes"/> agree on.</para>
    ///
    /// <para>⚠ The charge meter hangs off this — it sits on this same baseline, right of the digits
    /// (<see cref="ChargeMeterCenter"/>), so moving the score moves both.</para>
    ///
    /// <para>The craft and its payload sweep through this band (the keel's nose reaches 0.752 × field), and
    /// both draw <b>after</b> the scoreboard, so they pass over the number at 12 o'clock. That is accepted and
    /// predates the score moving up: the annulus is the only clear band there is, and the player parks the
    /// craft away from it. The real floor is the limit ring at 0.596 × field, which is why the meter can't
    /// simply drop instead.</para></summary>
    private static Point ScoreAnchor(Point c, double fieldRadius) =>
        new(c.X, c.Y - fieldRadius * 0.88);

    /// <summary>The live score, the combo readout, and the bomb charge, as one cluster.</summary>
    private static void DrawScoreboard(DrawingContext dc, Point c, double fieldRadius, Connate game, double ppd)
    {
        if (game.Phase is Connate.Stage.Intro or Connate.Stage.GameOver) return;
        Point anchor = ScoreAnchor(c, fieldRadius);

        // ── The counter takes the hit ──
        // Connate.ScorePulse is kicked by each arriving nugget and decays between them, so the number swells
        // and warms once per landing. Without it the digits merely change, and a number that changes on its
        // own is the weakest possible ending for the game's only scoring event.
        double pulse = Math.Clamp(game.ScorePulse, 0, 1);

        // ── The plate ──
        // One row hung in from the top of the disc, like Internode's HUD: the next tile, the score, the bomb
        // meter. The two slots are the meter's size and sit symmetric about the digits.
        double slot = ChargeMeterRadius(fieldRadius);
        Point meter = ChargeMeterCenter(anchor, fieldRadius, game, ppd);
        Point next  = new(anchor.X - (meter.X - anchor.X), anchor.Y);
        double rowH = Math.Max(2 * slot, ScoreText(game, fieldRadius, ppd, 0).Height);
        double padX = slot * 0.9, padY = slot * 0.35;
        // The stage caption sits under the row, inside the plate, so the plate grows down by its height.
        FormattedText stage = StageText(game, fieldRadius, ppd);
        double captionH = stage.Height * 0.8;
        var body = new Rect(next.X - slot - padX, anchor.Y - rowH / 2 - padY,
                            (meter.X + slot + padX) - (next.X - slot - padX), rowH + padY * 2 + captionH);
        ArcadeChrome.DrawHangingPlate(dc, body, fieldRadius * ArcadeChrome.PlateHang, ConnatePalette.Plaque, ConnatePalette.PlaquePen);
        ArcadeChrome.DrawInkCentered(dc, stage, new Point(anchor.X, anchor.Y + rowH / 2 + padY * 0.5 + captionH * 0.45));

        // ── The next tile ──
        // What the payload takes after the loaded piece fires: a queued bomb shows as the bomb itself.
        var (nextBomb, nextRank, nextHue) = game.NextPreview();
        if (nextBomb) DrawBomb(dc, next, slot, game.PhaseTime);
        else if (nextRank >= 0) DrawOrb(dc, next, slot, nextRank, nextHue, ppd, 0, 0);

        if (pulse > 0.02)
        {
            // A ring thrown off the counter, expanding as the punch decays — the visible splash of impact.
            var splash = new Pen(new SolidColorBrush(Color.FromArgb(
                (byte)(190 * pulse), ConnatePalette.MoteHotInk.R, ConnatePalette.MoteHotInk.G,
                ConnatePalette.MoteHotInk.B)), Math.Max(1, fieldRadius * 0.011 * pulse));
            splash.Freeze();
            double r = fieldRadius * (0.045 + (1 - pulse) * 0.055);
            dc.DrawEllipse(null, splash, anchor, r, r);
        }

        ArcadeChrome.DrawInkCentered(dc, ScoreText(game, fieldRadius, ppd, pulse), anchor);

        DrawChargeMeter(dc, anchor, fieldRadius, game, ppd);
    }

    /// <summary>The score counter's laid-out text. Shared with <see cref="ChargeMeterCenter"/>, which sits the
    /// meter beside it and so has to measure the same thing the frame is about to draw.</summary>
    private static FormattedText ScoreText(Connate game, double fieldRadius, double ppd, double pulse) =>
        ArcadeChrome.Text(((long)Math.Round(game.DisplayedScore)).ToString("N0"),
                          ArcadeChrome.Ui(Math.Max(9, fieldRadius * 0.085)) * (1 + pulse * ScorePulseGrowth),
                          ConnatePalette.ScoreInk(pulse), ppd, TextAlignment.Left);

    /// <summary>How much the counter swells at full pulse. Read by <see cref="ChargeMeterCenter"/> too — the
    /// meter's clearance is defined against this, so the two can't drift apart.</summary>
    private const double ScorePulseGrowth = 0.22;

    /// <summary>The stage caption under the score row: Internode's STAGE string, so every locale already has
    /// it. Sage ink while relief is on — the one visible tell that garbage is paused and the clock is longer —
    /// and the plate's dim ink otherwise. No extra word for relief: a new UI string would need four locales.</summary>
    private static FormattedText StageText(Connate game, double fieldRadius, double ppd) =>
        ArcadeChrome.Text(string.Format(Loc.T(UiText.Arcade.StageN), game.DifficultyStage),
                          ArcadeChrome.Ui(Math.Max(6, fieldRadius * 0.036)),
                          game.ReliefActive ? ConnatePalette.Aim : ConnatePalette.InkDim, ppd, TextAlignment.Left);

    /// <summary>The bomb charge, as a ghost bomb that fills. It is the same object that then flies into the
    /// payload, so the reward's whole journey is one shape rather than two unrelated widgets.
    ///
    /// <para>The wedge count is the live <see cref="Connate.BombChargeCost"/>, so a dearer bomb at a higher
    /// stage re-slices the pie on its own: the same earned charge visibly covers less of it.</para></summary>
    private static void DrawChargeMeter(DrawingContext dc, Point anchor, double fieldRadius, Connate game,
                                        double ppd)
    {
        Point c = ChargeMeterCenter(anchor, fieldRadius, game, ppd);
        double r = ChargeMeterRadius(fieldRadius);
        int slices = Math.Max(1, game.BombChargeCost);
        double flash = MeterFlash(game);

        // The ghost: the whole bomb at low opacity, so the shape of what you're filling is always readable.
        dc.PushOpacity(0.22);
        DrawBomb(dc, c, r, game.PhaseTime);
        dc.Pop();

        // The fill: the same bomb at full strength, clipped to a pie of the earned slices. Drawing the real
        // thing through a wedge — rather than painting a wedge in some accent colour — is what makes it read
        // as the bomb solidifying instead of as a gauge that happens to sit on one.
        int earned = Math.Clamp(game.BombCharge, 0, slices);
        if (earned > 0)
        {
            double sweep = Math.PI * 2 * earned / slices;
            // Radius 1.5× so the wedge comfortably contains the bomb's fuse and spark, which sit outside its
            // body — clipping to exactly r would shear them off.
            double reach = r * 1.6;
            var pie = new PathFigure { StartPoint = c, IsClosed = true, IsFilled = true };
            pie.Segments.Add(new LineSegment(new Point(c.X, c.Y - reach), true));
            int arcs = sweep > Math.PI ? 2 : 1;
            for (int i = 1; i <= arcs; i++)
            {
                double a = sweep * i / arcs;
                pie.Segments.Add(new ArcSegment(
                    new Point(c.X + Math.Sin(a) * reach, c.Y - Math.Cos(a) * reach),
                    new Size(reach, reach), 0, false, SweepDirection.Clockwise, true));
            }
            var wedge = new PathGeometry([pie]); wedge.Freeze();
            dc.PushClip(wedge);
            DrawBomb(dc, c, r, game.PhaseTime);
            dc.Pop();
        }

        if (flash > 0)
            dc.DrawEllipse(null, ConnatePalette.WhitePen(255, Math.Max(1, r * 0.22 * flash)),
                           c, r * (1.15 + flash * 0.5), r * (1.15 + flash * 0.5));

        // No divider lines between the earned wedges and no row of spare bombs: a queued bomb shows up as the
        // next tile in the HUD plate instead (DrawScoreboard).
    }

    /// <summary>The combo banner: big, central, struck through with the cel outline the rest of the game uses,
    /// escalating in colour with the count. A combo happens while the player's eyes are on the merge in the
    /// middle of the board, so it must be obvious there rather than tucked under the score.
    ///
    /// <para>⚠ Drawn last, over everything.</para></summary>
    private static void DrawComboBanner(DrawingContext dc, Point c, double fieldRadius, Connate game, double ppd)
    {
        // Two merges off one shot is the smallest thing worth calling a combo — the first merge is just the
        // shot working. So the banner's first appearance reads "COMBO ×2".
        if (game.ComboCount < 2 || game.ComboDisplayLeft <= 0) return;
        double life = Math.Clamp(game.ComboDisplayLeft / Math.Max(0.05, ConnateTuning.ComboDisplaySeconds), 0, 1);
        double age = 1 - life;

        // A back-eased pop on arrival, then a slow drift upward. The overshoot is what makes it read as
        // landing rather than appearing.
        double pop = age < 0.16 ? 1.35 - 0.35 * (age / 0.16) : 1.0 + Math.Sin(age * Math.PI) * 0.05;
        double rise = fieldRadius * 0.10 * age;
        // The last third fades; before that it stays fully opaque, so the number is never hard to read while
        // the chain it belongs to is still resolving.
        double alpha = Math.Clamp(life / 0.34, 0, 1);

        // Escalation: sage → gold → orange as the chain deepens. Colour carries "how big" pre-attentively;
        // the digit carries the exact number for anyone who looks.
        Color ink = game.ComboCount >= 5 ? ConnatePalette.FuseWarn
                  : game.ComboCount >= 3 ? Color.FromRgb(0xF6, 0xC4, 0x53)
                  : Color.FromRgb(0xA8, 0xD8, 0xBE);

        // Play is live under this one, so it keeps the unscaled size and sits up between the heap's limit
        // ring and the score rather than over the pieces the player is aiming at.
        DrawShout(dc, c, fieldRadius, Loc.F(UiText.Arcade.Combo, game.ComboCount), ink, age, pop, rise, alpha,
                  0.115, ppd, verticalOffset: -0.50, hud: false);
    }

    /// <summary>The board-clear bonus: the field is empty and the run's score has just been multiplied.
    ///
    /// <para>Uses the same routine as the combo banner, never a lookalike, so the game has exactly one way of
    /// shouting. It is bigger, sits at the centre of the now-empty field rather than above the heap, and takes
    /// white: the combo escalation ramp tops out at orange, and this outranks any of them.</para></summary>
    private static void DrawBoardClearBanner(DrawingContext dc, Point c, double fieldRadius, Connate game,
                                             double ppd)
    {
        if (game.BoardClearLeft <= 0) return;
        double life = Math.Clamp(
            game.BoardClearLeft / Math.Max(0.05, ConnateTuning.BoardClearDisplaySeconds), 0, 1);
        double age = 1 - life;
        double pop = age < 0.16 ? 1.35 - 0.35 * (age / 0.16) : 1.0 + Math.Sin(age * Math.PI) * 0.05;
        double alpha = Math.Clamp(life / 0.30, 0, 1);
        Color ink = Color.FromRgb(0xFF, 0xFF, 0xFF);

        // Two lines: what happened, then what it paid. The multiplier is formatted from the constant, so
        // retuning it can't leave the banner claiming the old number.
        DrawShout(dc, c, fieldRadius, Loc.T(UiText.Arcade.BoardClear), ink, age, pop, fieldRadius * 0.05 * age, alpha,
                  0.125, ppd, verticalOffset: -0.12);
        DrawShout(dc, c, fieldRadius,
                  $"BONUS {ConnateTuning.BoardClearMultiplier:0.0#}×", ink, 1, pop, fieldRadius * 0.05 * age,
                  alpha, 0.095, ppd, verticalOffset: 0.02);
    }

    /// <summary>The game's one shout voice (<see cref="ArcadeChrome.DrawShout"/>), centred
    /// <paramref name="verticalOffset"/> field radii below the disc centre and lifted by <paramref name="rise"/>.</summary>
    /// <param name="hud">True for a shout over a field nothing is happening on (the board clear), which takes
    /// the HUD text scale; false for one the player is still playing under, which stays at its own size.</param>
    private static void DrawShout(DrawingContext dc, Point c, double fieldRadius, string message, Color ink,
                                  double age, double pop, double rise, double alpha, double sizeFraction,
                                  double ppd, double verticalOffset, bool hud = true) =>
        ArcadeChrome.DrawShout(dc, new Point(c.X, c.Y + fieldRadius * verticalOffset - rise), fieldRadius, message,
                               ink, age, pop, alpha, sizeFraction, ppd, ArcadeChrome.ShoutPaint.Exact, hud);

    /// <summary>Where the bomb-charge meter sits. Shared by the meter, the combo sparks that fly into it and
    /// the bomb that flies out of it — separate copies of this arithmetic would let them miss each other.
    ///
    /// <para>It sits to the right of the counter, on its baseline — so the gap is measured off the score's
    /// laid-out width rather than guessed, and a seven-figure score can't grow into it. The measurement uses
    /// the swollen width (<see cref="ScorePulseGrowth"/>) even at rest, so a mote landing makes the counter
    /// pulse in place instead of shoving the meter sideways once per landing.</para></summary>
    private static Point ChargeMeterCenter(Point anchor, double fieldRadius, Connate game, double ppd) =>
        new(anchor.X + ScoreText(game, fieldRadius, ppd, 0).Width / 2.0 * (1 + ScorePulseGrowth)
                     + fieldRadius * ChargeMeterGap + ChargeMeterRadius(fieldRadius),
            anchor.Y);

    /// <summary>Clear space between the counter's widest extent and the meter's rim.</summary>
    private const double ChargeMeterGap = 0.045;

    private static double ChargeMeterRadius(double fieldRadius) => ArcadeChrome.UiArt(fieldRadius * 0.0385);

    /// <summary>0..1 flash for the meter, while any combo spark has just landed in it.</summary>
    private static double MeterFlash(Connate game)
    {
        double best = 0;
        foreach (ConnateComboBurst burst in game.ComboBursts)
        {
            double t = burst.Age / Math.Max(0.05, ConnateTuning.ComboBurstSeconds);
            if (t < ConnateTuning.ComboSparkArrivalFraction) continue;
            double after = (t - ConnateTuning.ComboSparkArrivalFraction)
                / Math.Max(0.01, 1 - ConnateTuning.ComboSparkArrivalFraction);
            best = Math.Max(best, Math.Clamp(1 - after, 0, 1));
        }
        return best;
    }

    /// <summary>The combo itself — a shockwave and a rising count at the merge, and a spark that flies
    /// from there into the bomb-charge pip it filled. The spark is the whole point of the effect: without a
    /// visible line from the cascade to the meter, the meter looks like it moves on its own.</summary>
    private static void DrawComboBursts(DrawingContext dc, Point c, double world, double fieldRadius,
                                        Connate game, double ppd)
    {
        Point anchor = ScoreAnchor(c, fieldRadius);
        foreach (ConnateComboBurst burst in game.ComboBursts)
        {
            double t = Math.Clamp(burst.Age / Math.Max(0.05, ConnateTuning.ComboBurstSeconds), 0, 1);
            Point origin = Screen(c, world, burst.X, burst.Y);

            // Shockwave: fast, thin, gone within the first third.
            double wave = Math.Clamp(t / 0.34, 0, 1);
            if (wave < 1)
            {
                double r = fieldRadius * (0.03 + wave * 0.15);
                dc.DrawEllipse(null, ConnatePalette.WhitePen(255, Math.Max(1, fieldRadius * 0.012 * (1 - wave))),
                    origin, r, r);
            }

            // The count, popping then rising and fading — the classic "×3" beat.
            double pop = t < 0.18 ? 1 + (1 - t / 0.18) * 0.55 : 1;
            double rise = fieldRadius * 0.10 * t;
            byte ink = (byte)Math.Clamp(255 * (1 - t * t), 0, 255);
            // Over live pieces, so it keeps its own size rather than the HUD scale.
            FormattedText count = ArcadeChrome.Text($"×{burst.Count}",
                Math.Max(7, fieldRadius * 0.055) * pop, ConnatePalette.WhiteBrush(ink), ppd, TextAlignment.Left);
            ArcadeChrome.DrawInkCentered(dc, count, new Point(origin.X, origin.Y - rise));

            // The spark, on an arc into its pip. Eased so it leaves lazily and arrives fast, which is what
            // makes the pip's flash feel caused rather than coincidental.
            double flight = Math.Clamp(t / Math.Max(0.01, ConnateTuning.ComboSparkArrivalFraction), 0, 1);
            if (flight >= 1) continue;
            // Every spark lands in the meter itself: a wedge of a pie has no single point to hit.
            // ConnateComboBurst.PipIndex is deliberately unread — snapshot-adjacent presentation data that
            // costs nothing to keep.
            Point pip = ChargeMeterCenter(anchor, fieldRadius, game, ppd);
            double ease = flight * flight;
            var p = new Point(origin.X + (pip.X - origin.X) * ease, origin.Y + (pip.Y - origin.Y) * ease);
            // A sideways bow so the spark doesn't travel a dead-straight line through the board.
            double bow = Math.Sin(flight * Math.PI) * fieldRadius * 0.10;
            p = new Point(p.X + bow * 0.35, p.Y - bow);
            double size = Math.Max(1, fieldRadius * 0.016 * (1 - flight * 0.4));
            dc.DrawEllipse(ConnatePalette.Merge, null, p, size, size);
            dc.DrawEllipse(null, ConnatePalette.AimStroke(255, Math.Max(1, size * 0.5)), p, size * 1.9, size * 1.9);
        }
    }

    /// <summary>Value from an exploded tile, flying to the counter — the run's payoff, since collecting is the
    /// only way Connate scores at all.
    ///
    /// <para>Two distinct beats, not one blended curve: the nuggets are flung out of the blast and coast
    /// (ease-out), then the counter takes them (t², leaving lazily and arriving fast). A single easing can say
    /// one of those things, never both, and it's the pair that makes the counter look like it reached out.</para>
    ///
    /// <para>Each nugget's stagger comes from the sim (<c>ConnateScoreMote.Delay</c>), so a big tile pays as a
    /// lengthening stream that ticks the counter over and over, ending on its biggest piece.</para></summary>
    private static void DrawScoreMotes(DrawingContext dc, Point c, double world, double fieldRadius, Connate game)
    {
        // The motes fly into the craft, not the counter: the payout lands on the player. The digits still take
        // the pulse from the sim's arrival clock, so number and craft answer the same beat.
        Point anchor = Polar(c, world * ConnateTuning.CraftOrbitRadius * CraftInset, game.PlayerAngle);
        double flight = Math.Max(0.05, ConnateTuning.ScoreMoteFlightSeconds);
        foreach (ConnateScoreMote mote in game.ScoreMotes)
        {
            // Scatter first. Once it is spent this clamps at 1, which freezes the launch point the pull then
            // interpolates from — the two beats hand over without a seam.
            double scatter = mote.Delay <= 0 ? 1 : Math.Clamp(mote.Age / mote.Delay, 0, 1);
            double coast = 1 - Math.Pow(1 - scatter, 2.4);
            Point from = Screen(c, world, mote.X + mote.DriftX * 0.17 * coast,
                                          mote.Y + mote.DriftY * 0.17 * coast);
            double pull = Math.Clamp((mote.Age - mote.Delay) / flight, 0, 1);

            double radius = Math.Max(1.5,
                fieldRadius * ConnateTuning.ScoreMoteRadiusFraction * Math.Max(0.05, mote.Size));
            // Tumble: seeded per nugget so a burst doesn't rotate in lockstep, and fast enough that the facets
            // actually catch — a still gem at this size is a dot.
            double spin = 1.6 + (mote.Seed % 5) * 0.55;
            double turn = mote.Seed * 0.7 + mote.Age * spin * ((mote.Seed & 1) == 0 ? 1 : -1) * Math.PI;
            // Only the last stretch fades, so the nugget is solid gold right up to the counter and its arrival
            // reads as a landing rather than a dissolve.
            double alpha = pull < 0.86 ? 1 : 1 - (pull - 0.86) / 0.14;

            Point p = PullPoint(from, anchor, pull, fieldRadius, (mote.Seed & 2) == 0 ? 1 : -1);

            dc.PushOpacity(Math.Clamp(alpha, 0, 1));

            // A comet of its own past positions while it's being pulled, so the stream reads as lines of gold
            // converging on the number rather than as loose specks near it.
            if (pull > 0.02)
                for (int ghost = 1; ghost <= 3; ghost++)
                {
                    double lag = pull - ghost * 0.05;
                    if (lag <= 0) break;
                    Point g = PullPoint(from, anchor, lag, fieldRadius, (mote.Seed & 2) == 0 ? 1 : -1);
                    dc.PushOpacity(0.30 - ghost * 0.07);
                    dc.DrawEllipse(ConnatePalette.Mote, null, g,
                                   radius * (0.72 - ghost * 0.12), radius * (0.72 - ghost * 0.12));
                    dc.Pop();
                }

            double glow = radius * 2.5;
            dc.DrawEllipse(ConnatePalette.MoteGlow, null, p, glow, glow);
            Geometry nugget = Nugget(p, radius, turn);
            dc.DrawGeometry(ConnatePalette.Mote, null, nugget);
            // Glassed like the tiles and outlined like everything else: the payout has to look like it was
            // drawn by the same hand as the board, or it reads as an effect pasted over the game.
            DrawGlass(dc, nugget);
            dc.DrawGeometry(null, ConnatePalette.MoteEdge, nugget);
            dc.DrawEllipse(ConnatePalette.MoteHot, null,
                           new Point(p.X - radius * 0.28, p.Y - radius * 0.30), radius * 0.20, radius * 0.20);

            dc.Pop();
        }
    }

    /// <summary>Where a nugget is at <paramref name="pull"/> of its flight. Shared with the trail ghosts, which
    /// have to trace the same bowed path — a straight-line trail behind a curved flight is worse than none.</summary>
    private static Point PullPoint(Point from, Point anchor, double pull, double fieldRadius, int side)
    {
        double ease = pull * pull;
        var p = new Point(from.X + (anchor.X - from.X) * ease, from.Y + (anchor.Y - from.Y) * ease);
        double dx = anchor.X - from.X, dy = anchor.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 1e-6) return p;
        // A sideways bow, so a burst arrives as curving strands instead of as spokes of a wheel.
        double bow = Math.Sin(Math.Clamp(pull, 0, 1) * Math.PI) * fieldRadius * 0.075 * side;
        return new Point(p.X + -dy / length * bow, p.Y + dx / length * bow);
    }

    /// <summary>One nugget's silhouette: six facets with alternating reach.
    ///
    /// <para>⚠ Deliberately not a disc with a rim. A coin is the obvious shape for collected value and is
    /// exactly wrong here — this board is already made of round tokens, so a round payout would read as tiles
    /// leaving rather than as treasure. A rough crystal shares nothing with the pieces it came from.</para></summary>
    private static Geometry Nugget(Point center, double radius, double rotation)
    {
        const int facets = 6;
        var vertices = new Point[facets];
        for (int i = 0; i < facets; i++)
        {
            double angle = rotation + i * Math.PI * 2 / facets;
            double reach = radius * (i % 2 == 0 ? 1.0 : 0.76);
            vertices[i] = new Point(center.X + Math.Cos(angle) * reach, center.Y + Math.Sin(angle) * reach);
        }
        return Polygon(vertices);
    }

    /// <summary>An earned bomb flying from the charge meter into the payload.
    ///
    /// <para>Arcs rather than travelling straight — a straight line between two HUD points reads as a UI
    /// transition, an arc reads as a thing being thrown. It spins and grows into place, and the meter throws
    /// a ring after it so the causal end of the journey is as clear as the arrival. Firing is refused for the
    /// duration (<c>Connate.BombDelivering</c>), so the bomb can never be launched from a rail it hasn't
    /// reached.</para></summary>
    private static void DrawBombDelivery(DrawingContext dc, Point c, double fieldRadius, Connate game,
                                         double world, double ppd)
    {
        if (!game.BombDelivering) return;
        double t = Math.Clamp(game.BombDeliveryProgress, 0, 1);
        Point from = ChargeMeterCenter(ScoreAnchor(c, fieldRadius), fieldRadius, game, ppd);
        Point to = Polar(c, world * ConnateTuning.LaunchRadius, game.PlayerAngle);

        // Ease-out so it leaves fast and settles, and a perpendicular bow so the path is a throw.
        double ease = 1 - Math.Pow(1 - t, 2.2);
        var p = new Point(from.X + (to.X - from.X) * ease, from.Y + (to.Y - from.Y) * ease);
        double dx = to.X - from.X, dy = to.Y - from.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 1e-6)
        {
            double bow = Math.Sin(t * Math.PI) * fieldRadius * 0.16;
            p = new Point(p.X + (-dy / len) * bow, p.Y + (dx / len) * bow);
        }

        // A trail of fading ghosts behind it, so the path itself is visible rather than just the endpoint.
        for (int i = 1; i <= 4; i++)
        {
            double lag = Math.Max(0, t - i * 0.055);
            double le = 1 - Math.Pow(1 - lag, 2.2);
            var g = new Point(from.X + (to.X - from.X) * le, from.Y + (to.Y - from.Y) * le);
            if (len > 1e-6)
            {
                double b = Math.Sin(lag * Math.PI) * fieldRadius * 0.16;
                g = new Point(g.X + (-dy / len) * b, g.Y + (dx / len) * b);
            }
            double gr = fieldRadius * 0.016 * (1 - i * 0.18);
            dc.DrawEllipse(BombGhosts[i - 1], null, g, gr, gr);
        }

        // Grows from meter-pip size to full payload size, spinning as it goes.
        double size = fieldRadius * 0.016 + (ConnateTuning.BombRadius * world - fieldRadius * 0.016) * ease;
        dc.PushTransform(new RotateTransform(t * 540, p.X, p.Y));
        DrawBomb(dc, p, size, game.PhaseTime, art: true);
        dc.Pop();

        // The launch flash at the meter, and the catch flash at the payload.
        if (t < 0.30)
        {
            double k = 1 - t / 0.30;
            dc.DrawEllipse(null, ConnatePalette.WhitePen(255, Math.Max(1, fieldRadius * 0.010 * k)),
                           from, fieldRadius * (0.02 + (1 - k) * 0.05), fieldRadius * (0.02 + (1 - k) * 0.05));
        }
        if (t > 0.72)
        {
            double k = (t - 0.72) / 0.28;
            dc.DrawEllipse(null, ConnatePalette.WhitePen(255, Math.Max(1, fieldRadius * 0.014 * (1 - k))),
                           to, size * (1 + k * 1.6), size * (1 + k * 1.6));
        }
    }

    /// <summary>The flying bomb's four trailing ghosts, its body colour fading from 72 to 18 alpha.</summary>
    private static readonly Brush[] BombGhosts =
        [.. Enumerable.Range(1, 4).Select(i => Frozen(new SolidColorBrush(Color.FromArgb((byte)(90 - i * 18), 0x0E, 0x10, 0x18))))];

    private static void DrawShotClock(DrawingContext dc, Point center, double radius, double progress,
                                      double fieldRadius, double time)
    {
        // ── Nothing until the last third, then a filled sweep ──
        // The timer is physically attached to the held piece rather than being detached HUD, and stays wholly
        // absent until the deadline is genuinely close — a ring on screen for the whole shot becomes furniture
        // instead of a warning. The caller passes ShotClockUrgency, which is 0 until the final stretch and
        // then 0→1 across just that stretch, so this draws the last third at full resolution.
        progress = Math.Clamp(progress, 0, 1);
        if (progress <= 0.002) return;

        // A filled disc sweeping round, not an arc: at this size a stroke reads as decoration on the payload,
        // where a wedge eating its way round the piece is unmistakably a countdown.
        var wedge = new PathFigure { StartPoint = center, IsClosed = true, IsFilled = true };
        double swept = progress * Math.PI * 2;
        wedge.Segments.Add(new LineSegment(new Point(center.X, center.Y - radius), true));
        int arcs = swept > Math.PI ? 2 : 1;
        for (int i = 1; i <= arcs; i++)
        {
            double a = swept * i / arcs;
            wedge.Segments.Add(new ArcSegment(
                new Point(center.X + Math.Sin(a) * radius, center.Y - Math.Cos(a) * radius),
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
        }
        var wedgeGeo = new PathGeometry([wedge]); wedgeGeo.Freeze();

        // Ramps toward the fuse's danger colours as it closes, so the two urgency readouts speak one language.
        dc.DrawGeometry(ArcadePalette.ExactSolid(ConnatePalette.FuseColour(progress), (byte)(120 + 110 * progress)),
                        ConnatePalette.LabelPen, wedgeGeo);
    }

    /// <summary>How wide garbage art is drawn against the lump's nominal radius: the furthest a lump's own
    /// twelve-point hash ever reaches, so a drawing that fills its canvas matches the biggest bump rather
    /// than the average. Art is not clipped, so this is alignment, not a limit.</summary>
    private const double GarbageArtReach = 1.05;

    private static void DrawGarbage(DrawingContext dc, Point center, double radius, long id, double time,
                                    double rotation = 0, double overPulse = -1)
    {
        // Stable ID hashing produces a consistent individual silhouette without storing twelve vertex offsets.
        // The tiny global-time breath supplies life but never changes the body's circular collision radius.
        const int points = 12;
        var vertices = new Point[points];
        double breathing = 1 + Math.Sin(time * 1.7 + id * 0.31) * 0.018;
        // ⚠ The `+ rotation` here is the same term ConnatePhysics.BuildGarbageLump uses. Both must move
        // together — the collider now traces this silhouette, so a turn applied to one and not the other
        // would put the shape and the thing it hits with out of step.
        for (int i = 0; i < points; i++)
        {
            double angle = i * Math.PI * 2 / points + rotation;
            ulong hash = (ulong)(id * 0x9E3779B1L + i * 0x45D9F3BL);
            double lump = 0.83 + (hash & 255) / 255.0 * 0.22;
            vertices[i] = new Point(center.X + Math.Cos(angle) * radius * lump * breathing,
                center.Y + Math.Sin(angle) * radius * lump * breathing);
        }
        Geometry blob = Polygon(vertices);
        dc.DrawGeometry(ConnatePalette.Garbage, null, blob);
        // Glassed like the tiles, so the board is one material. Garbage stays the darkest thing on it — the
        // gloss is a light on a fill, not a change of fill, so a blob still reads as the inert obstruction it
        // is rather than joining a family. Skipped when the lump wears art: the sprite drawn below covers it,
        // so the clip and its gradient fills were paid for nothing.
        if (!ArcadeSprites.Has(ArcadeSprites.Slot.ConnateGarbage)) DrawGlass(dc, blob);
        dc.DrawGeometry(null,
            overPulse >= 0 ? ConnatePalette.OverLimitPen(overPulse) : ConnatePalette.GarbagePen, blob);
        // The crater and glint ride the rotation too — they're the surface features that make the turn
        // readable at all on a lump this close to round.
        double crater = radius * 0.20;
        Point Feature(double dx, double dy) => new(
            center.X + (dx * Math.Cos(rotation) - dy * Math.Sin(rotation)) * radius,
            center.Y + (dx * Math.Sin(rotation) + dy * Math.Cos(rotation)) * radius);
        // Replacement art sits on the lump and is not clipped to it: the twelve-point silhouette is a
        // collider, not a frame the drawing has to fit inside, so art is free to overrun it. The vector body
        // and its edge stay underneath — the lump's own outline is what says which blob is which, and every
        // blob's is different. Turned with the lump, which is what makes the rotation readable at all on a
        // shape this close to round; the crater and glint below are the stand-ins art replaces.
        if (ArcadeSprites.Get(ArcadeSprites.Slot.ConnateGarbage) is { } art)
        {
            dc.PushTransform(new RotateTransform(rotation * 180 / Math.PI, center.X, center.Y));
            ArcadeSprites.Draw(dc, art, ArcadeSprites.Box(center, radius * GarbageArtReach));
            dc.Pop();
            return;
        }
        dc.DrawEllipse(ConnatePalette.GarbageInset, null, Feature(-0.25, -0.17), crater, crater * 0.76);
        dc.DrawEllipse(ConnatePalette.GarbageGlint, null, Feature(0.28, 0.22), radius * 0.10, radius * 0.08);
    }

    /// <summary>Clearing garbage shatters it — it is the one purely good thing that happens on this board, so
    /// it must not read as the blob quietly deflating.
    ///
    /// <para>The shards are cut from <b>this blob's own silhouette</b> — same twelve-point lump hash
    /// <see cref="DrawGarbage"/> builds it from — so the pieces are visibly the thing that was there, not a
    /// generic burst. Every piece is derived from the id and the age, so nothing is stored and nothing is
    /// random.</para></summary>
    private static void DrawGarbageClear(DrawingContext dc, Point c, double world, ConnateGarbageClear clear)
    {
        double t = Math.Clamp(clear.Age / Math.Max(0.01, ConnateTuning.GarbageClearSeconds), 0, 1);
        Point center = Screen(c, world, clear.X, clear.Y);
        double radius = clear.Radius * world;

        // The crack: a hard bright ring in the first fifth, gone before the shards have travelled far.
        double flash = Math.Clamp(1 - t / 0.20, 0, 1);
        if (flash > 0)
        {
            double r = radius * (0.9 + (1 - flash) * 0.85);
            dc.DrawEllipse(null, ConnatePalette.WhitePen(255, Math.Max(1, radius * 0.16 * flash)),
                center, r, r);
        }

        // Ease-out travel: the pieces leave fast and coast, which is what makes a break read as a break
        // rather than as a slow bloom.
        double travel = 1 - Math.Pow(1 - t, 2.6);

        // With art on the board the blob's own picture breaks up; the vector shards below are the fallback
        // for a board that has no sprite to break.
        if (ArcadeSprites.Get(ArcadeSprites.Slot.ConnateGarbage) is { } art)
        {
            // A harder ease than the vector shards: the pieces leave at speed and are visibly coasting by
            // the time they reach the edge, rather than still flying flat out as they go.
            DrawGarbageSpriteShatter(dc, art, center, radius, world / WorldToFieldScale, clear,
                                     1 - Math.Pow(1 - t, GarbagePieceEase));
            DrawGarbageDust(dc, center, radius, clear.GarbageId, t, travel);
            return;
        }

        const int points = 12;

        // ── The shards break again, then scale away ──
        // A split second in, each wedge fractures into three smaller pieces that keep going on their own
        // headings. ⚠ Every piece shrinks to nothing rather than fading: a fading shard reads as smoke, and on
        // a cel-shaded board a fading black outline is the most obviously wrong thing on screen.
        double split = ConnateTuning.GarbageSecondBreakFraction;
        bool broken = t >= split;
        double after = broken ? (t - split) / Math.Max(0.01, 1 - split) : 0;

        for (int i = 0; i < points; i++)
        {
            ulong hash = (ulong)(clear.GarbageId * 0x9E3779B1L + i * 0x45D9F3BL);
            double speed = 0.55 + (hash & 255) / 255.0 * 1.15;
            double spin = ((hash >> 8 & 255) / 255.0 - 0.5) * 520;   // degrees per unit of travel
            double bisector = (i + 0.5) * Math.PI * 2 / points;
            double flight = radius * speed * travel;

            // One wedge of the original silhouette: centre plus the two vertices either side of it.
            var wedge = new Point[3];
            wedge[0] = center;
            for (int v = 0; v < 2; v++)
            {
                int index = (i + v) % points;
                double angle = index * Math.PI * 2 / points;
                ulong lumpHash = (ulong)(clear.GarbageId * 0x9E3779B1L + index * 0x45D9F3BL);
                double lump = 0.83 + (lumpHash & 255) / 255.0 * 0.22;
                wedge[v + 1] = new Point(center.X + Math.Cos(angle) * radius * lump,
                                         center.Y + Math.Sin(angle) * radius * lump);
            }
            Point pivot = new((wedge[0].X + wedge[1].X + wedge[2].X) / 3,
                              (wedge[0].Y + wedge[1].Y + wedge[2].Y) / 3);

            // ── The wedge's outline as a six-point ring ──
            // Its three corners, with a midpoint between each pair pulled in or pushed out by its own hash.
            // Cutting fragments from runs of these gives them kinked edges and differing vertex counts, so a
            // break scatters bits of a shattered lump rather than a set of identical triangles.
            var ring = new Point[6];
            for (int k = 0; k < 3; k++)
            {
                ring[k * 2] = wedge[k];
                Point a = wedge[k], b = wedge[(k + 1) % 3];
                ulong midHash = hash ^ ((ulong)k * 0xA24BAED4963EE407UL);
                double bulge = 0.84 + (midHash & 255) / 255.0 * 0.36;
                ring[k * 2 + 1] = new Point(pivot.X + ((a.X + b.X) / 2 - pivot.X) * bulge,
                                            pivot.Y + ((a.Y + b.Y) / 2 - pivot.Y) * bulge);
            }

            // Before the second break: one whole lump, the ring itself. After it: three fragments, each cut
            // from its own run of the ring and each a different shape — the hash picks a triangle, a quad or
            // a five-pointed sliver with a notch on the pivot side.
            int pieces = broken ? 3 : 1;
            for (int f = 0; f < pieces; f++)
            {
                Point[] shape;
                if (pieces == 1) shape = ring;
                else
                {
                    ulong shapeHash = hash ^ ((ulong)f * 0xD6E8FEB86659FD93UL);
                    Point r0 = ring[f * 2 % 6], r1 = ring[(f * 2 + 1) % 6], r2 = ring[(f * 2 + 2) % 6];
                    shape = (shapeHash >> 24 & 3) switch
                    {
                        // Straight across, ignoring the midpoint.
                        0 => [pivot, r0, r2],
                        // A notch on the pivot side, cut back toward the centre.
                        1 =>
                        [
                            pivot, r0, r1, r2,
                            new Point(pivot.X + (r2.X - pivot.X) * 0.42, pivot.Y + (r2.Y - pivot.Y) * 0.42),
                        ],
                        // The run as authored, kink included.
                        _ => [pivot, r0, r1, r2],
                    };
                }

                // Each fragment gets its own scatter and its own stagger, so they don't vanish in unison.
                ulong fragHash = hash ^ ((ulong)f * 0x9E3779B97F4A7C15UL);
                double fragAngle = bisector + ((fragHash & 255) / 255.0 - 0.5) * 1.6;
                double fragReach = broken ? radius * 0.55 * after * (0.4 + (fragHash >> 8 & 255) / 255.0) : 0;
                double stagger = (fragHash >> 16 & 255) / 255.0 * ConnateTuning.GarbageShardStagger;
                double shrink = 1 - Math.Clamp((t - stagger) / Math.Max(0.05, 1 - stagger), 0, 1);
                if (shrink <= 0.01) continue;

                var offset = new Vector(Math.Cos(bisector) * flight + Math.Cos(fragAngle) * fragReach,
                                        Math.Sin(bisector) * flight + Math.Sin(fragAngle) * fragReach);

                Geometry shard = Polygon(shape).Clone();
                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(shrink, shrink, pivot.X, pivot.Y));
                transform.Children.Add(new RotateTransform(spin * travel + f * 37, pivot.X, pivot.Y));
                transform.Children.Add(new TranslateTransform(offset.X, offset.Y));
                shard.Transform = transform;
                shard.Freeze();
                dc.DrawGeometry(ConnatePalette.Garbage, ConnatePalette.GarbagePen, shard);
            }
        }

        DrawGarbageDust(dc, center, radius, clear.GarbageId, t, travel);
    }

    /// <summary>How many pieces the garbage sprite breaks into: five about an off-centre origin and ten
    /// around them, out to the sprite's edge. Changing it means re-deriving the ring layout below.</summary>
    private const int GarbageSpritePieces = 15;

    /// <summary>Ease-out exponent for the sprite pieces' flight. Higher front-loads the travel, so the
    /// pieces slow further before they clear the screen.</summary>
    private const double GarbagePieceEase = 4.5;

    /// <summary>The blob's art breaking up: the sprite as it was drawn, cut into <see cref="GarbageSpritePieces"/>
    /// jagged pieces that all fly outward past the field's edge, tumbling. Nothing shrinks or fades — the
    /// pieces leave the screen, which is what makes it read as a thing thrown apart rather than dissolving.
    ///
    /// <para>The cut is two rings about an origin displaced from the sprite's centre: an inner ring of five
    /// pieces meeting at that origin, and an outer ring of ten reaching well past the sprite box, so the
    /// corners are covered and the sprite's own alpha trims them. Ring radii are jittered per angle and every
    /// boundary point is shared by its two neighbours, so the pieces tile without gaps or overlaps. All of it
    /// is hashed off the garbage id; nothing is stored between frames.</para>
    ///
    /// <para><paramref name="fieldRadius"/> is how far a piece has to go to be off the round board from
    /// anywhere on it; every piece's flight is at least that plus the blob's own radius.</para></summary>
    private static void DrawGarbageSpriteShatter(DrawingContext dc, BitmapSource art, Point center, double radius,
                                                 double fieldRadius, ConnateGarbageClear clear, double travel)
    {
        double half = radius * GarbageArtReach;
        if (half < 1) return;
        Rect box = ArcadeSprites.Box(center, half);
        ulong seed = (ulong)(clear.GarbageId * 0x9E3779B1L) ^ 0x5851F42D4C957F2DUL;
        double Hash(int k) { unchecked { ulong h = seed + (ulong)k * 0x9E3779B97F4A7C15UL; h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32; return (h & 0xFFFFFF) / (double)0x1000000; } }

        const int outer = 10, inner = 5;
        // The origin every inner piece meets at, off centre so no piece is a regular wedge.
        double originAngle = Hash(1) * Math.PI * 2, originReach = half * (0.12 + Hash(2) * 0.2);
        var origin = new Point(center.X + Math.Cos(originAngle) * originReach,
                               center.Y + Math.Sin(originAngle) * originReach);
        // Twenty ring points — one at each outer boundary angle and one between — on a jittered radius, so
        // the seam between the two rings is a kinked line rather than a circle.
        var boundary = new double[outer];
        for (int j = 0; j < outer; j++)
            boundary[j] = j * Math.PI * 2 / outer + (Hash(10 + j) - 0.5) * 0.36;
        var ring = new Point[outer * 2];
        var far = new Point[outer * 2];
        for (int k = 0; k < outer * 2; k++)
        {
            int j = k / 2;
            double a = k % 2 == 0 ? boundary[j] : (boundary[j] + boundary[(j + 1) % outer] + (j == outer - 1 ? Math.PI * 2 : 0)) / 2;
            double r = half * (0.42 + Hash(40 + k) * 0.26);
            ring[k] = new Point(origin.X + Math.Cos(a) * r, origin.Y + Math.Sin(a) * r);
            // Far points sit well past the box so the piece covers the sprite's corner; the art's alpha does
            // the actual trimming.
            double fr = half * 2.4;
            double fa = a + (Hash(80 + k) - 0.5) * 0.18;
            far[k] = new Point(origin.X + Math.Cos(fa) * fr, origin.Y + Math.Sin(fa) * fr);
        }

        var pieces = new List<Point[]>(GarbageSpritePieces);
        // Inner: five pieces, each spanning two outer sectors, so four ring points plus the origin.
        for (int i = 0; i < inner; i++)
        {
            int k0 = i * 4;
            pieces.Add([origin, ring[k0], ring[(k0 + 1) % 20], ring[(k0 + 2) % 20], ring[(k0 + 3) % 20], ring[(k0 + 4) % 20]]);
        }
        // Outer: ten pieces between the ring and the far points.
        for (int j = 0; j < outer; j++)
        {
            int k0 = j * 2;
            pieces.Add([ring[k0], ring[k0 + 1], ring[(k0 + 2) % 20], far[(k0 + 2) % 20], far[k0 + 1], far[k0]]);
        }

        double lumpDegrees = clear.Rotation * 180 / Math.PI;
        double cos = Math.Cos(clear.Rotation), sin = Math.Sin(clear.Rotation);
        for (int i = 0; i < pieces.Count; i++)
        {
            Point[] shape = pieces[i];
            double cx = 0, cy = 0;
            foreach (Point q in shape) { cx += q.X; cy += q.Y; }
            var local = new Point(cx / shape.Length, cy / shape.Length);
            // The piece's centroid as it sat on screen: the sprite was drawn turned about the blob's centre.
            double dx = local.X - center.X, dy = local.Y - center.Y;
            var pivot = new Point(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);

            // Outward from the blob's centre through the piece, so the burst opens as a whole. A piece near
            // the centre borrows its ring angle rather than a degenerate direction.
            double heading = Math.Atan2(pivot.Y - center.Y, pivot.X - center.X);
            if (double.IsNaN(heading) || (dx * dx + dy * dy) < 1) heading = Hash(200 + i) * Math.PI * 2;
            heading += (Hash(220 + i) - 0.5) * 0.5;
            // Guaranteed past the edge by the end of the clear, from anywhere on the board.
            double distance = (fieldRadius + radius) * (1.02 + Hash(240 + i) * 0.18) * travel;
            double spin = (Hash(260 + i) - 0.5) * 900 * travel;

            dc.PushTransform(new TranslateTransform(Math.Cos(heading) * distance, Math.Sin(heading) * distance));
            dc.PushTransform(new RotateTransform(spin, pivot.X, pivot.Y));
            dc.PushTransform(new RotateTransform(lumpDegrees, center.X, center.Y));
            dc.PushClip(Polygon(shape));
            ArcadeSprites.Draw(dc, art, box);
            dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    /// <summary>Dust: smaller, faster, and sage rather than slate, so the burst carries the colour the game
    /// uses for "this went well" instead of being more grey specks. Shrinks away rather than fading.</summary>
    private static void DrawGarbageDust(DrawingContext dc, Point center, double radius, long garbageId,
                                        double t, double travel)
    {
        for (int i = 0; i < 10; i++)
        {
            ulong hash = (ulong)(garbageId * 0x2545F491L + i * 0x27220A95L);
            double angle = (hash & 1023) / 1023.0 * Math.PI * 2;
            double reach = radius * (1.15 + (hash >> 10 & 255) / 255.0 * 1.05) * travel;
            var p = new Point(center.X + Math.Cos(angle) * reach, center.Y + Math.Sin(angle) * reach);
            double stagger = (hash >> 18 & 255) / 255.0 * ConnateTuning.GarbageShardStagger;
            double shrink = 1 - Math.Clamp((t - stagger) / Math.Max(0.05, 1 - stagger), 0, 1);
            double size = radius * 0.075 * shrink;
            if (size <= 0.2) continue;
            // Slate for the odd mote, the garbage's own colour, so the dust reads as the blob's grit rather
            // than as white sparks.
            dc.DrawEllipse(i % 3 == 0 ? ConnatePalette.Garbage : ConnatePalette.Aim, null, p, size, size);
        }
    }

    private static void DrawAim(DrawingContext dc, Point c, double world, Connate game)
    {
        double heldRadius = game.HeldIsBomb ? ConnateTuning.BombRadius : ConnateTuning.RadiusForRank(game.HeldRank);
        double startRadius = ConnateTuning.LaunchRadius - heldRadius * 1.1;
        Point start = Polar(c, world * startRadius, game.PlayerAngle);
        Point end = Polar(c, world * game.AimContactRadius, game.PlayerAngle);
        double length = Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));
        // Opacity is measured against the full player-to-center path, not merely the currently visible segment.
        // Thus an early collision marker can remain visible, while an open ray is fully gone at two-thirds inward.
        double fadeDistance = world * startRadius * 2.0 / 3.0;
        double endOpacity = Math.Clamp(1 - length / Math.Max(1, fadeDistance), 0, 1);
        var gradient = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = start,
            EndPoint = end,
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0x81, 0xB2, 0x9A), 0));
        if (length > fadeDistance)
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x81, 0xB2, 0x9A), fadeDistance / length));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * endOpacity), 0x81, 0xB2, 0x9A), 1));
        gradient.Freeze();
        var aimPen = new Pen(gradient, 1.6); aimPen.Freeze();
        dc.DrawLine(aimPen, start, end);
        if (endOpacity > 0.02)
        {
            var endBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * endOpacity), 0x81, 0xB2, 0x9A));
            endBrush.Freeze();
            dc.DrawEllipse(endBrush, null, end, 2.2, 2.2);
        }
        if (length > 8)
        {
            for (int dot = 0; dot < 3; dot++)
            {
                double t = (game.PhaseTime * 1.8 + dot / 3.0) % 1.0;
                Point bead = new(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                double opacity = Math.Clamp(1 - length * t / Math.Max(1, fadeDistance), 0, 1);
                if (opacity <= 0.02) continue;
                dc.DrawEllipse(ConnatePalette.InkBrush((byte)(210 * opacity)), null, bead, 1.25, 1.25);
            }
        }
    }

    /// <summary>How much larger bomb art is drawn than the bomb's own radius. ⚠ Presentation only: the
    /// radius it is scaled from is the collider, and that is unchanged.</summary>
    private const double BombArtScale = 1.15;

    /// <summary>How far the loaded bomb is turned anticlockwise from the craft's facing, in degrees, so its
    /// fuse sits off to the side of the grips rather than straight up the muzzle. The casing and its fuse
    /// sparks read this together; bombs in flight and the meter's ghost do not lean.</summary>
    private const double LoadedBombLean = -40;

    /// <summary><paramref name="art"/> opts into replacement art, and only the bombs actually on the board
    /// pass it. The charge meter and its pip row are HUD furniture at a fraction of the size, where a drawing
    /// meant to read as an object on the field turns to mush; they keep the glyph.</summary>
    private static void DrawBomb(DrawingContext dc, Point p, double radius, double time, bool art = false)
    {
        // A bomb is black with a white glyph and belongs to neither tile family — it acts on everything
        // equally, so it must not borrow either family's hue (the sage aim colour would read as a merge
        // partner).
        // No shadow drawn here: a bomb in flight gets one from the heap's shadow pass; the payload and the
        // charge meter's copies are HUD furniture and cast none.
        // Replacement art takes the bomb whole — casing, glyph and spark. It is the one object that belongs
        // to neither family, so nothing here needs the palette's colour to survive.
        if (art && ArcadeSprites.Frame(ArcadeSprites.Slot.ConnateBomb) is { } face)
        {
            ArcadeSprites.Draw(dc, face, ArcadeSprites.Box(p, radius * BombArtScale));
            return;
        }
        dc.DrawEllipse(ConnatePalette.BombBody, null, p, radius, radius);
        // Glassed with everything else, so the one object the player is holding is not the only flat thing on
        // the board.
        var casing = new EllipseGeometry(p, radius, radius); casing.Freeze();
        DrawGlass(dc, casing);
        dc.DrawEllipse(null, ConnatePalette.BombEdge, p, radius, radius);

        // White glyph on the black body: a round casing with a fuse, drawn in the negative space.
        dc.DrawEllipse(ConnatePalette.BombGlyph, null, new Point(p.X, p.Y + radius * 0.10),
            radius * 0.43, radius * 0.43);
        Point fuseStart = new(p.X + radius * 0.22, p.Y - radius * 0.25);
        // The wick's run from the casing, at its original angle. The spark below rides the tip, so changing
        // this brings the spark with it rather than leaving it hanging off the end.
        Point fuseEnd = new(fuseStart.X + radius * 0.100, fuseStart.Y - radius * 0.135);
        dc.DrawLine(ConnatePalette.WhitePen(255, Math.Max(1.2, radius * 0.12), round: true), fuseStart, fuseEnd);
        // A six-armed asterisk at the wick's tip. ⚠ Still — it takes no `time` and must not start: the glyph
        // is an identity, not a timer, and a lit fuse that flickers on the charge meter reads as a countdown
        // this game does not have. The burning one is the loaded payload's, and that is DrawBombFuseSparks.
        // Arm length is the one knob; the stroke follows it, so the mark keeps its proportions when resized.
        double arm = radius * 0.126;
        Pen sparkPen = ConnatePalette.WhitePen(255, Math.Max(1, arm * 0.40), round: true);
        for (int i = 0; i < 3; i++)
        {
            double a = i * Math.PI / 3;
            double dx = Math.Cos(a) * arm, dy = Math.Sin(a) * arm;
            dc.DrawLine(sparkPen, new Point(fuseEnd.X - dx, fuseEnd.Y - dy),
                                  new Point(fuseEnd.X + dx, fuseEnd.Y + dy));
        }
    }

    /// <summary>Where the bomb art's yellow flame sits, in half-extents of the sprite box the art is drawn
    /// into — 0 is the box centre, ±1 an edge. The fuse sparks hang off this rather than the casing centre,
    /// so they land on the drawn flame instead of somewhere out on the sphere.
    /// ⚠ Measured off the yellowest pixels of <c>connate-bomb.png</c>. Re-measure if that art is
    /// replaced.</summary>
    private const double BombFlameX = 0.454;
    private const double BombFlameY = -0.795;

    /// <summary>Half-width of that flame, same units — the scale the shower is thrown at.</summary>
    private const double BombFlameSpread = 0.15;

    private const int BombFuseSparkCount = 9;
    private const double BombFuseSparkSeconds = 0.42;

    /// <summary>A spark's radius at birth, as a fraction of <see cref="BombFlameSpread"/>'s pixel width. The
    /// one knob for how fat the shower reads; the count and the reach are separate.</summary>
    private const double BombFuseSparkThickness = 0.52;

    /// <summary>The loaded bomb's fuse, spitting. Only the payload burns: a bomb still filling the charge
    /// meter isn't armed yet, one mid-delivery is already drawn with its own trail, and one in flight has a
    /// tail — so the shower marks the one bomb the player can actually fire.
    ///
    /// <para>⚠ Stateless. Every spark's position comes from <paramref name="time"/> and its own index,
    /// because this renderer draws a snapshot and owns no particles of its own; nothing here may accumulate
    /// between frames.</para></summary>
    /// <param name="facingDegrees">The casing's rotation about <paramref name="p"/>, clockwise, so the flame
    /// anchor stays on the art's fuse as the loaded bomb turns with the craft.</param>
    private static void DrawBombFuseSparks(DrawingContext dc, Point p, double radius, double time,
                                           double facingDegrees = 0)
    {
        // The anchor below is keyed to the art. With the fallback glyph on screen there is no flame to sit
        // on, and that drawing carries its own spark already.
        if (ArcadeSprites.Frame(ArcadeSprites.Slot.ConnateBomb) is null) return;
        double box = radius * BombArtScale;
        double spread = box * BombFlameSpread;
        if (spread < 0.6) return;
        double facing = facingDegrees * Math.PI / 180, cos = Math.Cos(facing), sin = Math.Sin(facing);
        double fx = box * BombFlameX, fy = box * BombFlameY;
        Point flame = new(p.X + fx * cos - fy * sin, p.Y + fx * sin + fy * cos);

        for (int i = 0; i < BombFuseSparkCount; i++)
        {
            // One cycle per spark, staggered so they don't all leave together. The generation index reseeds
            // the direction each time round, which is what stops the shower looping visibly.
            double cycle = time / BombFuseSparkSeconds + Scatter(i * 401);
            int generation = (int)Math.Floor(cycle);
            double life = cycle - generation;
            double angle = Scatter(i * 131 + generation * 17) * Math.PI * 2;
            double reach = spread * (1.5 + Scatter(i * 71 + generation * 29) * 2.3);
            // Coast out, lean up with the whole shower, then let gravity take it back — an expanding ring
            // reads as a shockwave, and it's the fall that reads as sparks.
            double coast = 1 - Math.Pow(1 - life, 2.2);
            var at = new Point(flame.X + Math.Cos(angle) * reach * coast,
                               flame.Y + Math.Sin(angle) * reach * coast * 0.7
                                       - reach * 0.85 * coast + reach * 1.5 * life * life);

            // ⚠ Cached, quantised brush — never one built here. Ten of these a frame is a brush every 1.7 ms
            // otherwise, and this board has to stay cheap while a game outside it eats the machine.
            double size = Math.Max(1.0, spread * BombFuseSparkThickness * (1 - life * 0.7));
            dc.DrawEllipse(ConnatePalette.FuseSpark(life), null, at, size, size);
        }

        // The flame itself flickers under the shower, on two out-of-step rates so it never finds a beat.
        double flicker = 0.55 + (Math.Sin(time * Math.PI * 11) + Math.Sin(time * Math.PI * 17.3)) * 0.11;
        dc.DrawEllipse(ConnatePalette.FuseGlow(flicker), null, flame, spread * flicker, spread * flicker);
    }

    /// <summary>A stable 0..1 from an integer. The fuse sparks' only source of variety, since the renderer
    /// keeps no state between frames and so cannot carry a random sequence.</summary>
    private static double Scatter(int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }

    private static void DrawBombExplosion(DrawingContext dc, Point c, double world, ConnateBombExplosion explosion)
    {
        // Presentation expands beyond the destroyed orb while the already-applied physics blast moves real bodies.
        double t = Math.Clamp(explosion.Age / Math.Max(0.01, ConnateTuning.BombExplosionSeconds), 0, 1);
        Point p = Screen(c, world, explosion.X, explosion.Y);
        double tile = ConnateTuning.RadiusForRank(explosion.DestroyedRank) * world;
        double ease = 1 - Math.Pow(1 - t, 3);
        byte alpha = (byte)(240 * (1 - t));
        double thickness = Math.Max(1.5, tile * (0.18 - t * 0.09));
        var pen = ConnatePalette.InkPen(alpha, thickness);
        double shock = tile * (0.65 + ease * 5.2);
        dc.DrawEllipse(null, pen, p, shock, shock);
        // A second, thinner ring chasing the shockwave: it starts a beat later and runs the same course, so
        // it trails inside the first and closes on it as both fade.
        double chaseT = Math.Clamp((t - BombChaseDelay) / (1 - BombChaseDelay), 0, 1);
        if (chaseT > 0)
        {
            double chaseEase = 1 - Math.Pow(1 - chaseT, 3);
            double chase = tile * (0.65 + chaseEase * 5.2);
            dc.DrawEllipse(null, ConnatePalette.InkPen(alpha, Math.Max(1, thickness * 0.35)), p, chase, chase);
        }
        if (t < 0.34)
            dc.DrawEllipse(ConnatePalette.InkDim, null, p, tile * (1.15 - t * 2.1), tile * (1.15 - t * 2.1));
        // The sparks are slivers pointed at both ends, widest at the middle, so they read as splinters
        // thrown from the blast rather than dashes stroked round it.
        Brush fill = ConnatePalette.InkBrush(alpha);
        double halfWidth = thickness * 0.5;
        for (int i = 0; i < 12; i++)
        {
            double angle = i * Math.PI * 2 / 12 + explosion.DestroyedRank * 0.37;
            double inner = tile * (0.7 + ease * 2.1);
            double outer = inner + tile * (0.55 + (i % 3) * 0.18) * (1 - t);
            double mid = (inner + outer) / 2;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            dc.DrawGeometry(fill, null, Polygon(
            [
                new Point(p.X + cos * inner, p.Y + sin * inner),
                new Point(p.X + cos * mid - sin * halfWidth, p.Y + sin * mid + cos * halfWidth),
                new Point(p.X + cos * outer, p.Y + sin * outer),
                new Point(p.X + cos * mid + sin * halfWidth, p.Y + sin * mid - cos * halfWidth),
            ]));
        }
    }

    /// <summary>How far into the bomb blast the chasing ring sets off, as a fraction of its life.</summary>
    private const double BombChaseDelay = 0.18;

    private static void DrawMotionTrail(DrawingContext dc, Point c, double world, ConnateBody body)
    {
        // Trails are birth-limited rather than permanent velocity streaks, keeping the rotating heap readable.
        double speed = Math.Sqrt(body.VelocityX * body.VelocityX + body.VelocityY * body.VelocityY);
        if (body.Age > 0.34 || speed < 0.18) return;
        double life = 1 - body.Age / 0.34;
        Point head = Screen(c, world, body.X, body.Y);
        double trailLength = world * Math.Min(0.12, speed * 0.075) * life;
        Point tail = new(head.X - body.VelocityX / speed * trailLength,
            head.Y - body.VelocityY / speed * trailLength);
        // Cached, quantised — this runs for every young moving body, and a blast makes that most of the heap.
        byte alpha = (byte)(110 * life);
        dc.DrawLine(ConnatePalette.InkPen(alpha, Math.Max(1, body.Radius * world * 0.18), round: true), tail, head);
    }

    /// <summary>Accelerating 0→1. The anticipation uses it so the reach is barely there at first contact and
    /// strongest at the moment of the merge, rather than peaking in the middle and easing back off.</summary>
    private static double EaseIn(double t) { t = Math.Clamp(t, 0, 1); return t * t; }

    private static void DrawBond(DrawingContext dc, Point c, double world, ConnateBody a, ConnateBody b,
                                 ConnatePendingMerge bond, double time)
    {
        // Moving beads and a growing midpoint make cause/effect readable during the short logical reservation.
        Point pa = Screen(c, world, a.X, a.Y);
        Point pb = Screen(c, world, b.X, b.Y);
        double progress = Smooth(bond.Progress);
        byte alpha = (byte)(70 + progress * 175);
        double width = Math.Max(1.2, Math.Min(a.Radius, b.Radius) * world * (0.10 + progress * 0.16));
        dc.DrawLine(ConnatePalette.AimStroke(alpha, width), pa, pb);

        double pulse = 0.55 + Math.Sin((time * 12 + bond.AId * 0.7) * Math.PI) * 0.20;
        for (int bead = 0; bead < 3; bead++)
        {
            double t = (time * (1.6 + progress) + bead / 3.0) % 1.0;
            Point p = new(pa.X + (pb.X - pa.X) * t, pa.Y + (pb.Y - pa.Y) * t);
            double radius = width * (0.65 + pulse * 0.35);
            dc.DrawEllipse(ConnatePalette.Ink, null, p, radius, radius);
        }

        Point midpoint = new((pa.X + pb.X) * 0.5, (pa.Y + pb.Y) * 0.5);
        double gather = Math.Min(a.Radius, b.Radius) * world * (0.10 + progress * 0.42);
        dc.DrawEllipse(ConnatePalette.Merge, null, midpoint, gather, gather);
    }

    /// <summary>How far into the merge flash its opacity peaks, as a fraction of its life. Short, so the
    /// burst arrives as a flash and spends most of its time fading.</summary>
    private const double MergeFlashAttack = 0.16;

    /// <summary>The flash's sparks are filled slivers pointed at both ends, not stroked lines, so they read
    /// as flying splinters rather than dashes. <paramref name="pen"/> still strokes the chain echo rings.</summary>
    private static void DrawMergeSparks(DrawingContext dc, Point center, double tileRadius,
                                        ConnateMergeFlash flash, double progress, Pen pen, byte alpha)
    {
        double flare = Math.Sin(Math.Clamp(progress / 0.72, 0, 1) * Math.PI);
        if (flare <= 0.01) return;
        int sparkCount = Math.Min(10, 5 + flash.ChainDepth);
        Brush fill = ConnatePalette.WhiteBrush(alpha);
        double halfWidth = Math.Max(0.6, tileRadius * 0.075);
        for (int i = 0; i < sparkCount; i++)
        {
            double angle = flash.Rank * 0.73 + i * Math.PI * 2 / sparkCount;
            double inner = tileRadius * (0.60 + progress * 1.45);
            double outer = inner + tileRadius * (0.24 + flare * 0.34);
            double mid = (inner + outer) / 2;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            dc.DrawGeometry(fill, null, Polygon(
            [
                new Point(center.X + cos * inner, center.Y + sin * inner),
                new Point(center.X + cos * mid - sin * halfWidth, center.Y + sin * mid + cos * halfWidth),
                new Point(center.X + cos * outer, center.Y + sin * outer),
                new Point(center.X + cos * mid + sin * halfWidth, center.Y + sin * mid - cos * halfWidth),
            ]));
        }

        for (int echo = 1; echo < Math.Min(3, flash.ChainDepth); echo++)
        {
            double delayed = progress - echo * 0.10;
            if (delayed is <= 0 or >= 1) continue;
            double rr = tileRadius * (0.9 + delayed * (1.4 + echo * 0.28));
            dc.DrawEllipse(null, pen, center, rr, rr);
        }
    }

    /// <summary>Fraction of the size fuse that passes before the outfield starts going red. The flood is the
    /// last-call reading of the same clock the fuse ring draws, so it must not start earlier than the ring's
    /// own dire colour or the two stop agreeing about how bad things are.</summary>
    private const double DoomFloodStart = 2.0 / 3.0;

    /// <summary>Over the fuse's last third the unsafe ground fills with dark red from the limit ring outward,
    /// reaching the field's edge exactly as the fuse runs out and the run ends. Ground, not chrome: it draws
    /// under the limit ring and under everything on the board.</summary>
    private static void DrawDoomFlood(DrawingContext dc, Point c, double limit, double field, double fuse)
    {
        double t = (Math.Clamp(fuse, 0, 1) - DoomFloodStart) / (1 - DoomFloodStart);
        if (t <= 0) return;
        double outer = limit + (field - limit) * Math.Min(1, t);
        var ring = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(c, outer, outer), new EllipseGeometry(c, limit, limit));
        ring.Freeze();
        dc.DrawGeometry(ConnatePalette.Doom, null, ring);
    }

    private static void DrawFuse(DrawingContext dc, Point c, double radius, double progress, double field)
    {
        // WPF ArcSegment cannot represent a full circle in one segment; split sweeps over π into two arcs.
        if (progress <= 0) return;
        // The band is a pure function of (progress, radius, field, centre) and GetWidenedPathGeometry is one of
        // the costlier calls in System.Windows.Media — it ran every frame the fuse burned. Progress is quantised
        // to 1/512 of a turn (~0.7°, under three pixels at the leading edge) and the widened band cached on it,
        // as is the ramp colour.
        int pq = (int)Math.Round(Math.Clamp(progress, 0, 1) * FuseSteps);
        if (pq <= 0) return;
        var key = (pq, (int)Math.Round(radius * 4), (int)Math.Round(field * 4), (int)Math.Round(c.X * 4), (int)Math.Round(c.Y * 4));
        if (!FuseBands.TryGetValue(key, out var band))
        {
            band = BuildFuseBand(c, radius, pq / (double)FuseSteps, field);
            if (FuseBands.Count >= 1024) FuseBands.Clear();
            FuseBands[key] = band;
        }
        if (!FuseFills.TryGetValue(pq, out var fill))
        {
            fill = new SolidColorBrush(ConnatePalette.FuseColour(pq / (double)FuseSteps)); fill.Freeze();
            FuseFills[pq] = fill;
        }
        dc.DrawGeometry(fill, ConnatePalette.LabelPen, band);
    }

    private const int FuseSteps = 512;
    private static readonly Dictionary<(int, int, int, int, int), Geometry> FuseBands = new();
    private static readonly Dictionary<int, Brush> FuseFills = new();

    private static Geometry BuildFuseBand(Point c, double radius, double progress, double field)
    {
        double sweep = Math.Clamp(progress, 0, 1) * Math.PI * 2;
        var figure = new PathFigure { StartPoint = new Point(c.X, c.Y - radius), IsFilled = false };
        int segments = sweep > Math.PI ? 2 : 1;
        for (int i = 1; i <= segments; i++)
        {
            double angle = sweep * i / segments;
            figure.Segments.Add(new ArcSegment(
                new Point(c.X + Math.Sin(angle) * radius, c.Y - Math.Cos(angle) * radius),
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
        }
        var geometry = new PathGeometry([figure]);

        // The ring ramps white → orange → red as it fills and carries the same outline everything else on this
        // board does.
        //
        // ⚠ The arc is a stroke, and a stroke can't itself be stroked — so it is widened into a closed band
        // first (GetWidenedPathGeometry turns the pen's outline into real geometry), then filled with the ramp
        // colour and outlined at the label weight. Don't fake it with a fatter black arc underneath: its ends
        // cap square across the sweep instead of following it, leaving the leading edge — the part you
        // actually read — with no outline at all.
        // Heavy on purpose (3× the old weight): this ring is the run's countdown, and at couch distance a
        // hairline arc was something you had to look for rather than something you noticed.
        double thickness = Math.Max(6, field * 0.042);
        var widen = new Pen(Brushes.Black, thickness)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        Geometry band = geometry.GetWidenedPathGeometry(widen);
        band.Freeze();
        return band;
    }

    /// <summary><paramref name="overPulse"/> is 0..1 while this tile sits outside the limit ring, or −1 when
    /// it doesn't — the one state that changes its outline rather than its fill. <paramref name="sweep"/> is
    /// 0..1 while a blended tile's shine is crossing it, −1 otherwise.
    ///
    /// <para>⚠ △ is consumed by the host (it opens the HOW TO PLAY card), so no tile-highlight assist can be
    /// driven from it here or in this routine's twin below.</para></summary>
    private static void DrawOrb(DrawingContext dc, Point p, double radius, int rank, int hue, double ppd,
                                double squash, double rotation, double swirl = 0,
                                double overPulse = -1, double sweep = -1)
    {
        if (rank is 0 or 1)
        {
            DrawFractionPiece(dc, p, radius, rank, hue, ppd, squash, rotation, swirl, overPulse);
            return;
        }

        // Squash preserves approximate area by inversely scaling Y. Numerals are drawn after Pop so they stay
        // upright and are centered by actual glyph ink bounds rather than font-layout whitespace.
        Brush fill = ConnatePalette.Tile(rank, hue);
        double radiusX = radius * Math.Clamp(1 + squash, 0.72, 1.30);
        double radiusY = radius * Math.Clamp(1 / Math.Max(0.72, 1 + squash), 0.72, 1.30);
        dc.PushTransform(new RotateTransform(rotation, p.X, p.Y));

        // Replacement art takes the whole tile — body, glass and the blend's swirl — because the family is
        // what a tile face has to say and only the artist's own three drawings can say it. Rank art first,
        // then the hue's, then the shared fallback.
        if (ArcadeSprites.First(0, ArcadeSprites.CycleFps, ArcadeSprites.Slot.ConnateOrbRank(rank),
                                OrbHueSlot(hue), ArcadeSprites.Slot.ConnateOrb) is { } face)
        {
            // Squash is applied as a transform rather than by fitting to an oval box: the tile deforms on
            // impact, and art fitted to the box would letterbox instead of stretching with it.
            dc.PushTransform(new ScaleTransform(radiusX / radius, radiusY / radius, p.X, p.Y));
            ArcadeSprites.Draw(dc, face, ArcadeSprites.Box(p, radius));
            dc.Pop();
            // The over-limit warning is the board's, not the art's — stroked on top so a tile past the line
            // still says so whatever it is wearing.
            if (overPulse >= 0) dc.DrawEllipse(null, ConnatePalette.OverLimitPen(overPulse), p, radiusX, radiusY);
            dc.Pop();
            DrawRankLabel(dc, p, Math.Min(radiusX, radiusY), rank, ppd);
            return;
        }
        // The piece's shadow is drawn by the heap's shadow pass, not here. Cel shading: every tile carries a
        // flat black outline.
        Pen rim = overPulse >= 0 ? ConnatePalette.OverLimitPen(overPulse) : ConnatePalette.CelPen;
        var body = new EllipseGeometry(p, radiusX, radiusY); body.Freeze();
        if (hue == ConnateRules.HueBlend)
        {
            // A blended tile carries both families, turning slowly. Drawn as a yin-yang because the shape says
            // "these two, together" without inventing a third colour that would read as a third family — and
            // because the S-curve makes the rotation legible on a plain disc.
            // The swirl only, no eye dots: at tile size they are two specks competing with the numeral for
            // the same few pixels, and the S-curve alone already says "both families".
            (Brush light, Brush dark) = ConnatePalette.BlendPair(rank);
            dc.DrawEllipse(light, null, p, radiusX, radiusY);
            dc.DrawGeometry(dark, null, BlendSwirl(p, radiusX, radiusY, swirl));
            // The glass sits over both lobes, so the two halves share one surface rather than reading as two
            // painted regions that happen to touch — and under the shine, which is a light on top of the
            // glass rather than a property of it.
            DrawGlass(dc, body);
            if (sweep >= 0) DrawBlendSweep(dc, p, radiusX, radiusY, sweep);
            dc.DrawEllipse(null, rim, p, radiusX, radiusY);
        }
        else
        {
            dc.DrawEllipse(fill, null, p, radiusX, radiusY);
            DrawGlass(dc, body);
            dc.DrawEllipse(null, rim, p, radiusX, radiusY);
        }

        dc.Pop();

        // ⚠ Drawn after dc.Pop(), i.e. outside the body's rotation: labels on tiles must always stay
        // right-side up. The platter turns the tile; the number does not turn with it.
        DrawRankLabel(dc, p, Math.Min(radiusX, radiusY), rank, ppd);
    }

    /// <summary>Which way the craft faces, in degrees, as a rotation for art drawn nose-up: inward, at the
    /// board, the way it shoots.
    ///
    /// <para>⚠ The clamps read this too. They are held by the craft, so its clock is their clock — written
    /// out twice, the two drifted half a turn apart the moment the craft was turned round.</para></summary>
    private static double CraftFacing(double angle) => angle * 180 / Math.PI + 180;

    /// <summary>How far out each clamp's centre sits, against the held piece's radius. 1 puts it exactly on
    /// the edge, so the art straddles it — which is what makes a clamp read as gripping rather than as a
    /// decal sitting on the face.</summary>
    private const double ClampRing = 1.0;

    /// <summary>How far in the loaded piece sits against the radius it launches from — tucked toward the
    /// craft's inward edge rather than balanced on its middle. The clamps are placed off the piece, so they
    /// come with it.
    ///
    /// <para>⚠ Presentation only. <c>ConnateTuning.LaunchRadius</c> is where a shot is actually born, so a
    /// piece leaving steps out to meet it — small, and under way, but that is the one place this shows.</para></summary>
    private const double PayloadInset = 0.90;

    /// <summary>How much further forward along the craft — inward, toward the board, the way it shoots — the
    /// loaded piece is seated, in world units on top of <see cref="PayloadInset"/>. Zero seats it exactly at
    /// the inset; negative pushes it back toward the rim.
    ///
    /// <para>⚠ World units, not pixels, so the seat holds at every disc size and DPI. On the 100% disc the
    /// playfield radius is 267 DIP less the 5.5% bezel, 252.3 px, and <c>WorldToFieldScale</c> 1.04 makes one
    /// world unit 262.4 px there — so the 4 DIP seated here is 0.0152 (10 DIP would be 0.038). Re-derive from that chain rather than re-measuring on
    /// whatever window happens to be open.</para></summary>
    private const double PayloadForward = 0.0152;   // 4 DIP

    /// <summary>A clamp's half-size against the held piece's radius.</summary>
    private const double ClampSize = 0.99;

    /// <summary>The grips holding the loaded piece, over the top of it.
    ///
    /// <para>Placed in the craft's frame, not the piece's: the loaded tile turns on its own axis while it
    /// waits, and a clamp that turned with it would be sliding round the thing it is supposed to be holding.
    /// So each one is offset along the craft's outward direction plus its clock angle, and turned by the
    /// same total — art drawn as the 12 o'clock clamp therefore points inward at every position.</para>
    ///
    /// <para>Nothing is drawn without art: there is no vector clamp to fall back to, and inventing one would
    /// change the board for everyone who has supplied no sprites.</para></summary>
    private static void DrawClamps(DrawingContext dc, Point held, double radius, double playerAngle)
    {
        if (radius < 2) return;
        double craft = CraftFacing(playerAngle);
        foreach ((double deg, string slot) in ArcadeSprites.Slot.ConnateClamps)
        {
            // A stable per-clamp phase, so a multi-frame clamp set never beats as one (docs/ARCADE.md ▸ sprite art).
            var art = ArcadeSprites.First(deg / 360.0, ArcadeSprites.CycleFps, slot, ArcadeSprites.Slot.ConnateClamp);
            if (art is null) continue;
            double total = craft + deg, rad = total * Math.PI / 180;
            var at = new Point(held.X + Math.Sin(rad) * radius * ClampRing,
                               held.Y - Math.Cos(rad) * radius * ClampRing);
            dc.PushTransform(new RotateTransform(total, at.X, at.Y));
            ArcadeSprites.Draw(dc, art, ArcadeSprites.Box(at, radius * ClampSize));
            dc.Pop();
        }
    }

    /// <summary>The per-hue sprite slot for one of the two shape ranks: rank 0 the star, rank 1 the socket
    /// it drops into. Ember or azure only — these two can never blend, the threshold is far above them.</summary>
    private static string StarSlot(int rank, int hue) => rank == 0
        ? hue == ConnateRules.HueEmber ? ArcadeSprites.Slot.ConnateStarEmber : ArcadeSprites.Slot.ConnateStarAzure
        : hue == ConnateRules.HueEmber ? ArcadeSprites.Slot.ConnateStarOpeningEmber
                                       : ArcadeSprites.Slot.ConnateStarOpeningAzure;

    /// <summary>The sprite slot for a hue. The families are the merge rule, so each gets its own drawing
    /// rather than one face tinted three ways.</summary>
    private static string OrbHueSlot(int hue) => hue switch
    {
        ConnateRules.HueEmber => ArcadeSprites.Slot.ConnateOrbEmber,
        ConnateRules.HueBlend => ArcadeSprites.Slot.ConnateOrbBlend,
        _                     => ArcadeSprites.Slot.ConnateOrbAzure,
    };

    /// <summary>The tile's value, bold with a black outline.
    ///
    /// <para>The outline is the reason this works on a cel-shaded board: a plain numeral has to pick one ink
    /// that reads against eight family tones, and there isn't one. White fill with a black stroke reads
    /// against all of them, and it matches the stroke language the tiles carry.</para></summary>
    private static void DrawRankLabel(DrawingContext dc, Point p, double radius, int rank, double ppd)
    {
        var glyphs = RankGlyphs(rank, radius, ppd);
        if (glyphs is null) return;
        // The cached outline is centred on its ink at the origin; one translate places it under the tile.
        dc.PushTransform(new TranslateTransform(p.X, p.Y));
        dc.DrawGeometry(ConnatePalette.Pip, ConnatePalette.LabelPen, glyphs);
        dc.Pop();
    }

    /// <summary>A tile numeral's outline, frozen and ink-centred on the origin, per (rank, size to ¼ px, dpi).
    /// Built per tile per frame this was a text layout, a glyph-outline extraction and a full geometry clone
    /// — ~2,400 outline builds a second on a twenty-tile board, for a dozen distinct strings. Geometry rather
    /// than DrawText because only a geometry carries the stroke, and centring on the ink rather than the
    /// layout box is what keeps digits with very different side bearings on the tile's centre.</summary>
    private static readonly Dictionary<(int Rank, int SizeQ, int PpdQ), Geometry?> RankGlyphCache = new();

    private static Geometry? RankGlyphs(int rank, double radius, double ppd)
    {
        string value = ConnateRules.ValueForRank(rank).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        double factor = value.Length switch { <= 2 => 0.86, 3 => 0.68, 4 => 0.55, _ => 0.43 };
        double size = Math.Max(6, radius * factor);
        var key = (rank, (int)Math.Round(size * 4), (int)Math.Round(ppd * 100));
        if (RankGlyphCache.TryGetValue(key, out var hit)) return hit;

        FormattedText text = ArcadeChrome.Text(value, size, ConnatePalette.Pip, ppd, TextAlignment.Left);
        text.SetFontWeight(FontWeights.Bold);
        Geometry built = text.BuildGeometry(new Point());
        Rect ink = built.Bounds;
        Geometry? centred = null;
        if (!ink.IsEmpty)
        {
            centred = built.Clone();
            centred.Transform = new TranslateTransform(-(ink.X + ink.Width / 2), -(ink.Y + ink.Height / 2));
            centred.Freeze();
        }
        if (RankGlyphCache.Count >= 512) RankGlyphCache.Clear();
        RankGlyphCache[key] = centred;
        return centred;
    }


    private static void DrawFractionPiece(DrawingContext dc, Point center, double radius, int rank, int hue, double ppd,
                                          double squash, double rotation, double swirl,
                                          double overPulse = -1)
    {
        _ = swirl;   // the 1 and 2 can never be blended — the auto-blend threshold is far above them
        // ── The 1 is a star, the 2 is a disc with a star-shaped hole ──
        //
        // A star dropping into a star-shaped socket says "these two make a 3" at a glance, where two arcs of
        // one circle would read as one broken thing rather than as two that fit.
        //
        // The 2's hole matches the star face (StarCutoutFraction tracks StarRadiusFraction), which is a
        // drawn-shape relationship only — the two colliders are one size. Neither carries pips: the shapes
        // are the label, and they are the only two ranks that aren't plain discs.
        double outer = rank == 0 ? radius : radius;
        Geometry shape = rank == 0
            ? StarGeometry(center, outer, ConnateTuning.StarInnerFraction)
            : DiscWithStarHole(center, radius, ConnateTuning.StarCutoutFraction,
                               ConnateTuning.StarInnerFraction);

        double scaleX = Math.Clamp(1 + squash, 0.78, 1.24);
        double scaleY = Math.Clamp(1 / Math.Max(0.78, 1 + squash), 0.78, 1.24);
        dc.PushTransform(new RotateTransform(rotation, center.X, center.Y));
        dc.PushTransform(new ScaleTransform(scaleX, scaleY, center.X, center.Y));

        // ⚠ The two ranks take replacement art differently, because their silhouettes differ. The star art is
        // the star itself — its own alpha is the outline, nothing around it — so it is drawn unclipped into
        // the tile box, points landing on the tile radius the pentagon collider is built from; a clip to
        // either the star outline or the disc would shave those points. The socket is a complete disc with
        // the star cut out of it, so it is clipped to that disc, which is also its collider.
        if (ArcadeSprites.First(0, ArcadeSprites.CycleFps, StarSlot(rank, hue),
                                rank == 0 ? ArcadeSprites.Slot.ConnateStar
                                          : ArcadeSprites.Slot.ConnateStarOpening) is { } face)
        {
            Geometry outline;
            if (rank == 0)
            {
                ArcadeSprites.Draw(dc, face, ArcadeSprites.Box(center, radius));
                outline = shape;
            }
            else
            {
                var disc = new EllipseGeometry(center, radius, radius);
                disc.Freeze();
                ArcadeSprites.DrawClipped(dc, face, disc);
                outline = disc;
            }
            if (overPulse >= 0) dc.DrawGeometry(null, ConnatePalette.OverLimitPen(overPulse), outline);
            dc.Pop();
            dc.Pop();
            return;
        }

        // No drop shadow — see DrawOrb.
        dc.DrawGeometry(ConnatePalette.Tile(rank, hue), null, shape);
        // The rim traces the star's points and the socket's inner edge, which is where this earns its keep:
        // a shaded-and-lit edge is what makes the 1 look like a solid it could actually drop into.
        DrawGlass(dc, shape);
        dc.DrawGeometry(null,
            overPulse >= 0 ? ConnatePalette.OverLimitPen(overPulse) : ConnatePalette.CelPen, shape);
        dc.Pop();
        dc.Pop();
        _ = ppd;
    }

    /// <summary>Gives every tile a glassy physicality, without transparency.
    ///
    /// <para>Three layers over the flat family fill, all of them opaque paint on an opaque tile: the bowed
    /// <see cref="ConnatePalette.Gloss"/> horizon, an inner edge that shades where the light lands and
    /// brightens on the far rim (<see cref="ConnatePalette.EdgeBounce"/>), and a hard
    /// <see cref="ConnatePalette.Specular"/> where the light actually hits. Gradient alone reads as a
    /// gradient; it's the inner rim that makes an object look thick, and the specular that makes it look wet.</para>
    ///
    /// <para>Takes a <b>geometry</b> rather than a radius, so one routine serves the disc, the star, the disc
    /// with a star socket, and a garbage lump — the rim traces whatever silhouette it's handed. Everything is
    /// drawn inside a clip of that shape, so nothing can escape the tile onto the board.</para>
    ///
    /// <para>⚠ Call before the cel outline. The outline is the drawing's ink and has to be the last thing on
    /// a tile; a gloss painted over it would soften exactly the edge the cel look depends on.</para></summary>
    private static void DrawGlass(DrawingContext dc, Geometry shape)
    {
        Rect bounds = shape.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 1 || bounds.Height <= 1) return;
        // Below this the glass cannot be read: a score mote is ~4 px across and its rim pen alone is 3 px
        // wide, so a bomb's forty motes paid a clip and three gradient fills each for a shimmer that was
        // already just a dot. The meter's bombs (~14 px) keep theirs.
        if (bounds.Width < 12 || bounds.Height < 12) return;

        dc.PushClip(shape);
        dc.DrawGeometry(ConnatePalette.Gloss, null, shape);

        // The inner rim: a thick stroke on the shape's own outline, with the outer half clipped away. Half of
        // a stroke always falls inside the path, which is what makes this trace a star's points and a
        // garbage lump's dents as readily as a circle.
        double rim = Math.Max(1.5, Math.Min(bounds.Width, bounds.Height) * 0.16);
        // Cached, quantised — never a Pen built here. This runs per tile per frame; see RimPen.
        dc.DrawGeometry(null, ConnatePalette.RimPen(rim * 2), shape);

        // The specular, up and left — the light's own position, so it agrees with the gloss above it. An
        // ellipse rather than a circle: a highlight on a curved surface is foreshortened, and a round one
        // reads as a sticker.
        var spot = new Point(bounds.X + bounds.Width * 0.32, bounds.Y + bounds.Height * 0.24);
        dc.DrawEllipse(ConnatePalette.Specular, null, spot, bounds.Width * 0.21, bounds.Height * 0.15);
        dc.Pop();
    }

    /// <summary>A five-pointed star, point up before rotation. <paramref name="innerFraction"/> is the waist
    /// radius as a fraction of <paramref name="outer"/>.</summary>
    private static Geometry StarGeometry(Point c, double outer, double innerFraction)
    {
        double inner = outer * Math.Clamp(innerFraction, 0.15, 0.9);
        var figure = new PathFigure { IsClosed = true, IsFilled = true };
        for (int i = 0; i < 10; i++)
        {
            double r = i % 2 == 0 ? outer : inner;
            double a = -Math.PI / 2 + i * Math.PI / 5;
            var pt = new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
            if (i == 0) figure.StartPoint = pt;
            else figure.Segments.Add(new LineSegment(pt, true));
        }
        var geometry = new PathGeometry([figure]); geometry.Freeze(); return geometry;
    }

    /// <summary>The 2: a full disc with a star cut out of the middle. Two figures in one geometry with the
    /// default EvenOdd fill — the star's winding cancels the disc's, so the hole is a real hole and the cel
    /// outline traces both edges, which is what makes the socket read as a socket.</summary>
    private static Geometry DiscWithStarHole(Point c, double radius, double holeFraction, double innerFraction)
    {
        var disc = new PathFigure { StartPoint = new Point(c.X + radius, c.Y), IsClosed = true, IsFilled = true };
        disc.Segments.Add(new ArcSegment(new Point(c.X - radius, c.Y), new Size(radius, radius), 0,
            true, SweepDirection.Clockwise, true));
        disc.Segments.Add(new ArcSegment(new Point(c.X + radius, c.Y), new Size(radius, radius), 0,
            true, SweepDirection.Clockwise, true));

        var star = (PathGeometry)StarGeometry(c, radius * Math.Clamp(holeFraction, 0.1, 0.95), innerFraction);
        var geometry = new PathGeometry([disc, .. star.Figures]);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>A shine crossing a blended tile: a diagonal band travelling corner to corner, clipped to the
    /// tile so it can never leak onto the board.
    ///
    /// <para>Brightest in the middle of its travel and gone at both ends, so it reads as light passing over
    /// rather than a panel sliding across. The band is angled rather than vertical for the same reason every
    /// foil card does it: a vertical wipe reads as a UI transition.</para></summary>
    private static void DrawBlendSweep(DrawingContext dc, Point p, double radiusX, double radiusY, double t)
    {
        var clip = new EllipseGeometry(p, radiusX, radiusY); clip.Freeze();
        dc.PushClip(clip);

        double span = radiusX * 2.6;                       // travel, corner to corner and out the far side
        double centre = -span / 2 + span * t;              // band's position along the sweep axis
        double width = radiusX * 0.42;
        // Fades in and out across the travel; a hard-edged band appearing at the rim reads as a glitch.
        byte peak = (byte)Math.Clamp(200 * Math.Sin(Math.Clamp(t, 0, 1) * Math.PI), 0, 255);

        Brush shine = ConnatePalette.BlendShine(peak);

        // 35° off vertical, drawn long enough that its ends are always outside the clip.
        dc.PushTransform(new RotateTransform(-35, p.X, p.Y));
        dc.DrawRectangle(shine, null,
            new Rect(p.X + centre - width / 2, p.Y - radiusY * 2.2, width, radiusY * 4.4));
        dc.Pop();
        dc.Pop();   // clip
    }

    /// <summary>The dark half of a yin-yang inscribed in the tile. One outer semicircle plus the two
    /// half-radius arcs that make the S — the classic construction, rotated by <paramref name="swirlDegrees"/>
    /// so the tile visibly turns. Elliptical radii are honoured so it survives the jelly squash intact.
    ///
    /// <para>⚠ Callers pass a negated angle to turn it clockwise. A positive WPF RotateTransform is clockwise
    /// on screen, but this figure's S-curve is built anticlockwise, so its apparent direction is the opposite
    /// of its transform's sign. Keep the negation at the call sites so the geometry stays the textbook
    /// construction.</para></summary>
    private static Geometry BlendSwirl(Point p, double radiusX, double radiusY, double swirlDegrees)
    {
        double rx = radiusX, ry = radiusY;
        var figure = new PathFigure
        {
            StartPoint = new Point(p.X, p.Y - ry), IsClosed = true, IsFilled = true,
        };
        figure.Segments.Add(new ArcSegment(new Point(p.X, p.Y + ry), new Size(rx, ry), 0,
            false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new ArcSegment(new Point(p.X, p.Y), new Size(rx / 2, ry / 2), 0,
            false, SweepDirection.Counterclockwise, true));
        figure.Segments.Add(new ArcSegment(new Point(p.X, p.Y - ry), new Size(rx / 2, ry / 2), 0,
            false, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry([figure])
        {
            Transform = new RotateTransform(swirlDegrees, p.X, p.Y),
        };
        geometry.Freeze();
        return geometry;
    }


    /// <summary>Half-extent of craft art against the vector craft's <c>size</c>, matching its widest reach
    /// (wingtip to wingtip) so a replacement occupies the rim the same way. Art is centred on the orbit
    /// point, which the vector hull straddles rather than balances on.</summary>
    private const double CraftArtHalf = 2.48;

    /// <summary>Where the craft's parts sit, in pixels of the 512-canvas frame <see cref="CraftArtHalf"/>
    /// spans, measured so the assembly reproduces the one-piece craft drawing: both part files are drawn at
    /// <see cref="CraftPartScale"/> of that canvas, the body's centre this far below the frame centre, and each
    /// wing's centre this far out to its side (the right wing is the left file mirrored). ⚠ Fitted to the
    /// shipped body and wing files; re-measure if either is re-exported at a different framing.</summary>
    private const double CraftPartScale = 0.4271;
    private const double CraftBodyOffsetY = 7.6;
    private const double CraftWingOffsetX = 147.0;
    private const double CraftWingOffsetY = 4.0;
    /// <summary>The point the left wing turns about, in the same canvas pixels, signed from the frame centre.
    /// Fitted to the three whole-craft frames: the wing's outer tip and its shoulder both keep one distance
    /// from this point across them, turning 5° then 16°. It sits up by the body's far shoulder rather than at
    /// the wing's own root, so the flap is mostly the wing sliding down the hull with a small tilt — the
    /// motion those frames draw. The right wing's pivot is its mirror.</summary>
    private const double CraftWingPivotX = 84.0;
    private const double CraftWingPivotY = -84.0;

    /// <summary>The wing flap, in degrees: how far forward (toward the nose) each wing turns at a full draw,
    /// and how far it swings away at the peak of the release. Both subtle on purpose — the wings are the
    /// bowstring's tell, not a wingbeat.</summary>
    private const double CraftWingDrawDegrees = 5.0;
    private const double CraftWingReleaseDegrees = 7.0;

    /// <summary>The wings' angle about their shoulders, positive forward, riding the same bow the hull and
    /// payload ride so the whole assembly moves as one thing. Scaled off <see cref="CraftDraw"/>: its draw
    /// depth maps to the forward angle and its release overshoot to the swing away, so the deadzone, the
    /// eased draw and the single-push return all carry over without a second curve to keep in step.</summary>
    private static double WingFlap(bool held, double sinceHold, double sinceFire, double shotTension, double autoWind)
    {
        double draw = CraftDraw(held, sinceHold, sinceFire, shotTension, autoWind);
        return draw >= 0
            ? draw / Math.Max(1e-6, CraftDrawDepth) * CraftWingDrawDegrees
            : draw / Math.Max(1e-6, CraftSpringDepth) * CraftWingReleaseDegrees;
    }

    /// <summary>The craft's idle rock, in world units of orbit radius. ⚠ Read by the payload as well as the
    /// craft: the loaded piece and its clamps are held by the craft, so anything that moves one has to move
    /// all three or the grip comes apart.</summary>
    private static double CraftBreath(double time) => Math.Sin(time * 3.4) * 0.0025;

    /// <summary>How far in from its orbit the craft is drawn. ⚠ The craft only — the payload and the clamps
    /// are placed off the launch radius and must not move with it.</summary>
    private const double CraftInset = 0.855;

    /// <summary>How far outward the whole craft assembly is pulled at full charge, in world units.
    /// ⚠ Modest on purpose: the round window is a hard clip (see <c>ArcadeControl</c>), so a deep draw eats
    /// the craft's tail against the bezel. Raise it with that in view.</summary>
    private const double CraftDrawDepth = 0.045;

    /// <summary>How far inward past rest the release overshoots, same units — the string's snap. Deliberately
    /// a fraction of the draw: the recoil has to read as the assembly settling, not as a second shot.</summary>
    private const double CraftSpringDepth = 0.016;

    /// <summary>Charge below which the bow does not move at all — a quick tap fires the orb and nothing else.
    /// ⚠ A hard gate, not a small pull: a few pixels of travel below it read as the craft twitching rather
    /// than drawing, and the release would spring from that twitch. Above the gate the draw ramps from zero
    /// across what is left, so there is no step at the threshold.
    /// At 0.5 s to full charge this is a hold under about 90 ms.</summary>
    private const double CraftDrawTapDeadzone = 0.18;

    /// <summary>How long the release takes to spring through rest and settle.
    /// ⚠ Must stay under Connate's recoil cap (1 s), where <c>SinceFire</c> stops counting — a spring longer
    /// than that would never reach its own end, and an untouched craft would sit permanently mid-recoil.</summary>
    private const double CraftSpringSeconds = 0.14;

    /// <summary>The bow draw: a radial offset in world units, positive outward, away from the board. Holding
    /// ✕ draws the assembly back; releasing snaps it forward through rest to a short overshoot and settles.
    ///
    /// <para>The draw tracks <c>ConnateTuning.FullChargeSeconds</c> rather than the wind-up animation, so the
    /// string's depth is the shot's power — full draw and full launch speed are reached together. That makes
    /// this the only tell for how charged a shot is; don't retime it to the sprite frames.</para>
    ///
    /// <para>⚠ Read by the craft, the payload and (through the payload) the clamps, exactly as
    /// <see cref="CraftBreath"/> is: they are one assembly, and anything that moves one has to move all three
    /// or the grip comes apart. Both clocks are the sim's, so a frozen board repaints as it froze.</para>
    ///
    /// <para>⚠ The spring reads <c>Connate.ShotDrawTension</c>, not the hold clock. The hold clock merely
    /// freezes at its last value, so a deadline shot would spring from a full draw the craft was never at and
    /// jerk the whole assembly outward on the frame the clock spent the piece. The sim records what the bow
    /// was actually at; see that property.</para>
    ///
    /// <para>⚠ A release the sim refuses — cooldown, blocked muzzle — cannot be animated: nothing fired, so
    /// <c>SinceFire</c> stays old and is indistinguishable from long-settled, and the assembly returns to rest
    /// in one frame. That matches the craft's own sprite, which likewise snaps back with no shot; animating it
    /// needs a refusal clock the sim does not keep.</para></summary>
    private static double CraftDraw(bool held, double sinceHold, double sinceFire, double shotTension,
                                    double autoWind)
    {
        // Eased so the draw is fastest in its first frames and stiffens toward the end — how a bow actually
        // resists, and what lets a moderate hold read as a draw rather than as nothing happening.
        // ⚠ Under CraftDrawTapDeadzone it returns a flat zero, not a small number. A tap fires the orb with
        // no bow at all: a sliver of travel there would read as a twitch, and a twitch on the object the eye
        // is already following is worse than no motion.
        static double Pull(double tension)
        {
            double t = Math.Clamp(tension, 0, 1);
            if (t < CraftDrawTapDeadzone) return 0;
            double over = (t - CraftDrawTapDeadzone) / (1 - CraftDrawTapDeadzone);
            return CraftDrawDepth * (1 - Math.Pow(1 - over, 2.2));
        }

        // The deadline winds the launcher on its own, so the bow answers whichever is deeper: the player's
        // hold, or the clock closing. Without the max a player holding through the final stretch would see
        // the draw they built jump to whatever the clock had reached.
        double charge = Math.Max(held ? sinceHold / Math.Max(1e-3, ConnateTuning.FullChargeSeconds) : 0,
                                 autoWind);
        // Mid-spring the auto-wind is still 0 (Fire resets the clock), so the release keeps the branch.
        double s = Math.Clamp(sinceFire / Math.Max(1e-3, CraftSpringSeconds), 0, 1);
        if (held || s >= 1) return Pull(charge);

        // From where the bow actually was, forward through rest to one overshoot and back — a single push, no
        // ringing. A string releases fast and the recoil settles; an oscillation reads as a loose mount.
        // ⚠ Pull() gates the overshoot too, through the ternary: a tapped shot springs nothing, because it
        // drew nothing.
        double release = 1 - Math.Pow(1 - s, 3);
        double from = Pull(shotTension);
        double spring = from <= 0 ? 0 : CraftSpringDepth * Math.Clamp(shotTension, 0, 1);
        return Math.Max(from * (1 - release), Pull(charge)) - spring * Math.Sin(s * Math.PI);
    }

    private static void DrawCraft(DrawingContext dc, Point c, double world, double angle, double tileRadius, double time,
                                 double sinceFire = double.PositiveInfinity, bool held = false,
                                 double sinceHold = double.PositiveInfinity, double shotTension = 0,
                                 double autoWind = 0)
    {
        // Local coordinates: t runs tangent to the rim; radial runs inward. This keeps one authored silhouette
        // correctly oriented for every player angle without bitmap assets or per-angle geometry.
        // Drawn a shade inside its orbit: the payload and its clamps keep the launch radius, so the craft
        // tucks under what it is holding rather than sitting level with it.
        // ⚠ The bow draw is added outside the inset, not inside it: scaled by CraftInset the craft would draw
        // back 14% less than the payload it is holding, and the string would stretch.
        Point center = Polar(c, world * ((ConnateTuning.CraftOrbitRadius + CraftBreath(time)) * CraftInset
                                         + CraftDraw(held, sinceHold, sinceFire, shotTension, autoWind)), angle);
        var outward = new Vector(Math.Sin(angle), -Math.Cos(angle));
        var inward = -outward;
        var tangent = new Vector(Math.Cos(angle), Math.Sin(angle));
        double size = tileRadius * 1.65;

        Point L(double t, double radial) => center + tangent * (t * size) + inward * (radial * size);

        // Replacement art is turned so its top points inward, at the board — the way the craft shoots, and so
        // the way its nose faces. One drawing serves every angle, exactly as the vector craft does. Its
        // frames play once per shot off the sim's own recoil clock, so a frozen board repaints identically.
        // A hold outranks the release: the wound pose lasts as long as the player holds it, where releasing
        // snaps straight back to rest. Held reads the sim, not the pad, so a frozen board repaints as it froze.
        //
        // The deadline winds the craft too, with ✕ untouched, so the pose agrees with the bow beside it —
        // a hull sitting at rest while its own launcher is visibly drawn back reads as a bug. It is fed the
        // wind-up's own elapsed time, scaled out of the auto-wind fraction, so the frames step at the rate
        // they were authored for rather than being stretched over the clock's window.
        double windTime = held ? sinceHold
            : autoWind > 0 ? autoWind * ArcadeSprites.Slot.ConnateCraftWind.Length / ArcadeSprites.ShotFps
            : double.NegativeInfinity;
        var pose = double.IsFinite(windTime)
            ? ArcadeSprites.Once(ArcadeSprites.Slot.ConnateCraft, windTime, ArcadeSprites.Slot.ConnateCraftWind)
            : ArcadeSprites.Once(ArcadeSprites.Slot.ConnateCraft, sinceFire, ArcadeSprites.Slot.ConnateCraftShot);
        // The parted craft outranks the pose frames: wings first, the body over them, all in the frame the
        // whole-craft art was fitted to so the assembly sits on the rim exactly where the one-piece drawing did.
        if (ArcadeSprites.Get(ArcadeSprites.Slot.ConnateCraftBody) is { } body
            && ArcadeSprites.Get(ArcadeSprites.Slot.ConnateCraftWing) is { } wing)
        {
            double half = size * CraftArtHalf;
            double px = half / 256;   // one pixel of the fitted 512 canvas
            dc.PushTransform(new RotateTransform(CraftFacing(angle), center.X, center.Y));
            Point At(double dx, double dy) => new(center.X + dx * px, center.Y + dy * px);
            double partHalf = half * CraftPartScale;
            // The wings flap about their shoulders with the bow: forward as it draws, a quick swing away as
            // it releases, back to rest. The right wing is the left one mirrored through the frame's centre,
            // flap included, so one drawing and one angle serve both.
            double flap = WingFlap(held, sinceHold, sinceFire, shotTension, autoWind);
            Point shoulder = At(CraftWingPivotX, CraftWingPivotY);
            Rect wingBox = ArcadeSprites.Box(At(-CraftWingOffsetX, CraftWingOffsetY), partHalf);
            dc.PushTransform(new RotateTransform(flap, shoulder.X, shoulder.Y));
            ArcadeSprites.Draw(dc, wing, wingBox);
            dc.Pop();
            dc.PushTransform(new ScaleTransform(-1, 1, center.X, center.Y));
            dc.PushTransform(new RotateTransform(flap, shoulder.X, shoulder.Y));
            ArcadeSprites.Draw(dc, wing, wingBox);
            dc.Pop(); dc.Pop();
            ArcadeSprites.Draw(dc, body, ArcadeSprites.Box(At(0, CraftBodyOffsetY), partHalf));
            dc.Pop();
            return;
        }

        if (pose is { } hull)
        {
            dc.PushTransform(new RotateTransform(CraftFacing(angle), center.X, center.Y));
            ArcadeSprites.Draw(dc, hull, ArcadeSprites.Box(center, size * CraftArtHalf));
            dc.Pop();
            return;
        }

        Geometry leftWing = Polygon([L(-0.10, 0.05), L(-1.12, -0.10), L(-1.55, 0.38),
            L(-0.72, 0.55), L(-0.18, 0.78)]);
        Geometry rightWing = Polygon([L(0.10, 0.05), L(1.12, -0.10), L(1.55, 0.38),
            L(0.72, 0.55), L(0.18, 0.78)]);
        dc.DrawGeometry(ConnatePalette.CraftTeal, ConnatePalette.CraftPen, leftWing);
        dc.DrawGeometry(ConnatePalette.CraftTeal, ConnatePalette.CraftPen, rightWing);

        Geometry keel = Polygon([L(0, -0.35), L(0.34, 0.28), L(0.18, 0.90),
            L(0, 1.18), L(-0.18, 0.90), L(-0.34, 0.28)]);
        dc.DrawGeometry(ConnatePalette.CraftBronze, ConnatePalette.CraftPen, keel);
        dc.DrawEllipse(ConnatePalette.CraftDark, null, L(0, 0.48), size * 0.23, size * 0.23);
        double corePulse = 0.075 + (Math.Sin(time * Math.PI * 4) + 1) * 0.018;
        dc.DrawEllipse(ConnatePalette.CraftLight, null, L(0, 0.48), size * corePulse, size * corePulse);

        dc.DrawEllipse(ConnatePalette.CraftBronze, null, L(-0.76, 0.25), size * 0.20, size * 0.20);
        dc.DrawEllipse(ConnatePalette.CraftBronze, null, L(0.76, 0.25), size * 0.20, size * 0.20);
    }

    private static void DrawIntro(DrawingContext dc, Point c, double field, double ppd)
    {
        dc.DrawEllipse(ConnatePalette.Scrim, null, c, field, field);
        ArcadeChrome.DrawCentered(dc, "CONNATE", ArcadeChrome.Ui(Math.Max(16, field * 0.12)), ConnatePalette.Ink,
            c.X, c.Y - field * 0.46, ppd, field * 1.5);
        // The card points at the how-to rather than compressing the rules into a tagline: the △ card is where
        // they actually live, and a slogan that half-explains them competes with it.
        ArcadeChrome.DrawCentered(dc, $"{ControllerButtons.Text(PadButton.Triangle)}  {Loc.T(UiText.Arcade.HowToPlay)}",
            ArcadeChrome.Ui(Math.Max(9, field * 0.052)), ConnatePalette.Aim,
            c.X, c.Y - field * 0.08, ppd, field * 1.6);
        ArcadeChrome.DrawCentered(dc, $"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Begin)}",
            ArcadeChrome.Ui(Math.Max(9, field * 0.048)), ConnatePalette.InkDim,
            c.X, c.Y + field * 0.16, ppd, field * 1.5);
    }

    private static void DrawGameOver(DrawingContext dc, Point c, double field, Connate game, double ppd)
    {
        dc.DrawEllipse(ConnatePalette.Scrim, null, c, field, field);
        ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.BoundaryCrossed), ArcadeChrome.Ui(Math.Max(14, field * 0.095)), ConnatePalette.Danger,
            c.X, c.Y - field * 0.50, ppd, field * 1.5);
        // Collected, not "field sum": the board itself is worth nothing, and this number is only what bombs
        // banked and the motes delivered.
        ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.Collected), ArcadeChrome.Ui(Math.Max(8, field * 0.045)), ConnatePalette.InkDim,
            c.X, c.Y - field * 0.22, ppd, field * 1.5);
        ArcadeChrome.DrawCentered(dc, game.FinalFieldSum.ToString("N0"), ArcadeChrome.Ui(Math.Max(15, field * 0.115)), ConnatePalette.Ink,
            c.X, c.Y - field * 0.10, ppd, field * 1.6);
        ArcadeChrome.DrawCentered(dc, $"BEST {game.HighScore:N0}", ArcadeChrome.Ui(Math.Max(9, field * 0.050)), ConnatePalette.Aim,
            c.X, c.Y + field * 0.20, ppd, field * 1.5);
        // Both ways out of the end screen, so ✕ is not the only door the card admits to.
        ArcadeChrome.DrawCenteredRow(dc,
            [$"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Again)}",
             $"{ControllerButtons.Text(PadButton.Circle)}  {Loc.T(UiText.Arcade.ExitGame)}"],
            ArcadeChrome.Ui(Math.Max(9, field * 0.047)), ConnatePalette.InkDim, c.X, c.Y + field * 0.40, ppd);
    }

    private static Point Screen(Point c, double scale, double x, double y) => new(c.X + x * scale, c.Y + y * scale);
    private static Point Polar(Point c, double radius, double angle) => ArcadePalette.Polar(c, radius, angle);

    private static double Smooth(double t) => ArcadeMath.Smoothstep(t);

    private static Geometry Polygon(IReadOnlyList<Point> points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        for (int i = 1; i < points.Count; i++) figure.Segments.Add(new LineSegment(points[i], true));
        var geometry = new PathGeometry([figure]); geometry.Freeze();
        return geometry;
    }

    // ── How-to-play illustrations ─────────────────────────────────────────────
    // One per bullet on the △ card, drawn with the board's own routines — DrawOrb, DrawBomb, DrawCraft —
    // rather than with simplified stand-ins. That matters more here than it would elsewhere: Connate's
    // rules are carried by shape and colour (a star and a socket that fit; two families that don't mix),
    // so a diagram that merely resembles a tile would be teaching the wrong thing. Everything is passed a
    // time of 0 and no squash, so the card is still.

    public void DrawHowToArt(DrawingContext dc, Rect box, string art, double ppd)
    {
        var c = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
        double r = Math.Min(box.Width, box.Height) / 2;

        switch (art)
        {
            // The craft alone. ⚠ No rim arc under it: at this size the stub of ring read as the limit ring,
            // which is the last panel's subject and the one thing on the card that means "you lost".
            case "craft":
            {
                double orbit = r * 1.9;
                var hub = new Point(c.X, c.Y - orbit + r * 0.72);
                // DrawCraft places itself from a hub and an angle; 6 o'clock is where the craft starts a run.
                // The divisor is floored because CraftOrbitRadius is a mutable tuning field — arcade-tuning
                // .json can set it to anything, and a zero here would put the craft at infinity.
                DrawCraft(dc, hub, orbit / Math.Max(0.05, ConnateTuning.CraftOrbitRadius), Math.PI, r * 0.30, 0);
                break;
            }

            // The 1 and the 2 side by side — a star and the socket it drops into. The two shapes visibly
            // fit, which is the whole reason they are shaped that way, and a line of text cannot show it.
            case "merge":
            {
                double t = r * 0.62;
                DrawOrb(dc, new Point(c.X - r * 0.62, c.Y), t, 0, ConnateRules.HueAzure, ppd, 0, 0);
                DrawOrb(dc, new Point(c.X + r * 0.62, c.Y), t, 1, ConnateRules.HueAzure, ppd, 0, 0);
                break;
            }

            // One tile from each family, so "same colour" has two named things to mean.
            case "families":
            {
                double t = r * 0.62;
                DrawOrb(dc, new Point(c.X - r * 0.62, c.Y), t, 2, ConnateRules.HueAzure, ppd, 0, 0);
                DrawOrb(dc, new Point(c.X + r * 0.62, c.Y), t, 2, ConnateRules.HueEmber, ppd, 0, 0);
                break;
            }

            case "bomb":
                DrawBomb(dc, c, r * 0.80, 0, art: true);   // the board's bomb art, not the vector fallback
                break;

            // The limit ring with a tile pressing through it, wearing the same over-limit warning outline
            // the board gives a strayed tile — so the failure state is recognised before it is met.
            case "limit":
            {
                var ring = new Pen(ConnatePalette.Limit, Math.Max(1, r * 0.11)); ring.Freeze();
                dc.DrawEllipse(null, ring, c, r * 0.86, r * 0.86);
                // ⚠ Azure, not ember. The warning is the outline (the last argument), not the family — an
                // ember tile here reads as "the red ones are the danger", which is not a rule this game has.
                DrawOrb(dc, new Point(c.X + r * 0.62, c.Y - r * 0.62), r * 0.46, 3,
                        ConnateRules.HueAzure, ppd, 0, 0, 0, 1);
                break;
            }
        }
    }
}
