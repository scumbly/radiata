using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Draws one game's state. The sim (in <c>Core</c>) knows nothing about this side; the renderer
/// casts to its own game type and reads its public state directly.</summary>
internal interface IArcadeRenderer
{
    /// <summary>The game's accent, used by the shared chrome (lives pips, hold rings) so a game's identity
    /// carries into the frame without the frame being redesigned per game.</summary>
    Brush Accent { get; }

    /// <summary>True when <see cref="Draw"/> paints its own opaque ground over the whole playfield, so the
    /// frame can skip its field fill and vignette — two full-disc paints a frame that were never seen.</summary>
    bool CoversField => false;

    /// <summary>Draw into a playfield disc of <paramref name="fieldRadius"/> centred at
    /// <paramref name="c"/>. The bezel is already painted and the disc-twist (shake) transform is already
    /// pushed by the host — the renderer draws in a still, upright frame.</summary>
    void Draw(DrawingContext dc, Point c, double fieldRadius, IArcadeGame game, double ppd);

    /// <summary>Draw one how-to-play bullet's illustration inside <paramref name="box"/>.
    /// <paramref name="art"/> is the key its <see cref="ArcadeHowToLine"/> named.
    ///
    /// <para>The host owns the card's layout and hands each renderer a box; the renderer draws whatever that
    /// key means to it, using its own palette — so a bullet about Kabloom's bees is drawn in Kabloom's
    /// colours and the card reads as belonging to the game rather than to the chrome.</para>
    ///
    /// <para>A default no-op: a game may want text-only bullets, and an unrecognised key must draw nothing
    /// rather than throw — this runs on the render pump, where an exception closes the arcade.</para></summary>
    void DrawHowToArt(DrawingContext dc, Rect box, string art, double ppd) { }
}

internal static class ArcadeRenderers
{
    /// <summary>Renderer for a game id, or null if the shell has none (which is a bug, not a state — the
    /// catalog and this map are added to together).</summary>
    public static IArcadeRenderer? For(string id) =>
        // Drop-in script games all share one renderer — the game is identified by manifest, not by id,
        // and the renderer only replays the session's validated command buffer.
        id.StartsWith(ScriptGameManifest.TokenPrefix, StringComparison.OrdinalIgnoreCase)
            ? ScriptGameRenderer.Instance
            : id switch
    {
        Kabloom.GameId => KabloomRenderer.Instance,
        Connate.GameId => ConnateRenderer.Instance,
        PetalPop.GameId => PetalPopRenderer.Instance,
        Internode.GameId   => InternodeRenderer.Instance,
        _              => null,
    };
}
