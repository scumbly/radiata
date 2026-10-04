using System.Collections.Generic;
using System.Linq;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The summon-gesture catalog's internal consistency. Every failure here is a gesture that is
/// OFFERED in the Settings builder but misbehaves at runtime in a way no build or smoke test catches:
/// a token with no <see cref="TriggerModes.Describe"/> entry shows its raw config string in a toast, one
/// with no <see cref="TriggerModes.EnableDisableLabel"/> teaches the user "the chord", and one the
/// interpreter has no case for can never open a wheel at all (it only traces, once). Pure Core.</summary>
internal static class T_Triggers
{
    private static readonly ControllerKind[] Kinds =
        [ControllerKind.DualSenseEdge, ControllerKind.PlayStationOther,
         ControllerKind.Xbox, ControllerKind.ExtraButtonPad];

    public static void Run()
    {
        H.Group("Trigger catalog (Core)");

        // Everything a user can actually BUILD in Settings, per kind.
        var offered = new List<(ControllerKind Kind, string Token)>();
        foreach (var kind in Kinds)
            foreach (var p in TriggerModes.PrimariesFor(kind))
                foreach (var m in TriggerModes.ModifiersFor(p, kind))
                {
                    var token = TriggerModes.Compose(p, m);
                    H.Check($"{kind}: {p}+{m} composes", token is not null);
                    if (token is not null) offered.Add((kind, token));
                }

        foreach (var (kind, token) in offered.DistinctBy(o => o.Token))
        {
            // A miss here means Describe fell through to the raw token (a toast showing the config
            // string) or EnableDisableLabel fell through (onboarding teaching "the chord").
            H.Check($"\"{token}\" has a spoken phrase", TriggerModes.Describe(token, kind) != token);
            H.Check($"\"{token}\" has an enable/disable phrase",
                    TriggerModes.EnableDisableLabel(token, kind) != "the chord");
            H.Check($"\"{token}\" round-trips", TriggerModes.TryDecompose(token, out var p2, out var m2)
                                                && TriggerModes.Compose(p2, m2) == token);
        }

        // Every offered chord must be one the interpreter can actually match. SidesFor is private, so
        // this asserts the same list from the outside: the two non-chord tokens are handled elsewhere
        // (fn by App's own handlers, the swipe by EvalTouch); everything else needs a case.
        string[] handledOutsideSidesFor = [TriggerModes.FnButtons, TriggerModes.TouchpadSwipe];
        string[] chordCases =
        [
            TriggerModes.BumpersHome, TriggerModes.TriggersHome, TriggerModes.BumpersTriggers,
            TriggerModes.BumpersStick, TriggerModes.TriggersStick, TriggerModes.ViewMenuStick,
            TriggerModes.ViewMenuBumpers, TriggerModes.ViewMenuTriggers,
            TriggerModes.BumpersDpad, TriggerModes.TriggersDpad,
            TriggerModes.ExtraTriggers, TriggerModes.ExtraBumpers, TriggerModes.ExtraHome, TriggerModes.ExtraStick,
            TriggerModes.ExtraSelectStart, TriggerModes.ExtraDpad,
        ];
        // A miss = no TriggerInterpreter.SidesFor case, i.e. a gesture that can never open a wheel.
        foreach (var token in offered.Select(o => o.Token).Distinct())
            H.Check($"\"{token}\" is matchable at runtime",
                    handledOutsideSidesFor.Contains(token) || chordCases.Contains(token));

        H.Check("Edge defaults to its Fn pair",
                TriggerModes.DefaultFor(ControllerKind.DualSenseEdge) == TriggerModes.FnButtons);
        // L4/R4 are ordinary buttons players bind in games, so they are OFFERED but never
        // claimed by default — unlike the Edge's Fn pair, which exists for nothing else.
        H.Check("extra-button pads do NOT default to L4/R4",
                TriggerModes.DefaultFor(ControllerKind.ExtraButtonPad) == TriggerModes.ViewMenuBumpers);
        H.Check("pads with no dedicated button default to Bumper + Select/Start",
                TriggerModes.DefaultFor(ControllerKind.Xbox) == TriggerModes.ViewMenuBumpers);

        // The lone-press token must be distinguishable from the chords that CONTAIN it: a lone L4/R4
        // press has to go dead while an L4/R4 + button chord is selected, or it pre-empts the chord.
        H.Check("a lone L4/R4 press is not an extra CHORD", !TriggerModes.IsExtraChord(TriggerModes.FnButtons));
        H.Check("L4/R4 + Select/Start is", TriggerModes.IsExtraChord(TriggerModes.ExtraSelectStart));
        H.Check("a bumper chord is not", !TriggerModes.IsExtraChord(TriggerModes.ViewMenuBumpers));

        // A lone L4/R4 press must stay the FIRST (default) option now that the chord rows exist.
        var extraMods = TriggerModes.ModifiersFor(TriggerPrimary.Fn, ControllerKind.ExtraButtonPad);
        H.Check("L4/R4's default modifier is None", extraMods.Count > 0 && extraMods[0] == TriggerModifier.None);
        H.Check("L4/R4 offers the standard second buttons", extraMods.Count > 1);
        H.Check("the Edge's Fn stays a lone summon",
                TriggerModes.ModifiersFor(TriggerPrimary.Fn, ControllerKind.DualSenseEdge).Count == 1);

        // Second-dropdown wording: only the empty pairing depends on the primary — a dash reads as an
        // empty cell next to a dedicated button. The shoulder rows stay bare.
        H.Check("L4/R4's \"no second button\" reads as (none)",
                TriggerModes.ModifierLabel(TriggerModifier.None, TriggerPrimary.Fn) == "(none)");
        H.Check("a bumper row's empty modifier still reads as a dash",
                TriggerModes.ModifierLabel(TriggerModifier.None) == "—");
        H.Check("L4/R4's shoulder rows are labelled plainly",
                TriggerModes.ModifierLabel(TriggerModifier.Bumper, TriggerPrimary.Fn) == "Bumper"
                && TriggerModes.ModifierLabel(TriggerModifier.Trigger, TriggerPrimary.Fn) == "Trigger");

        // Kind-aware naming: the same token reads as the buttons the pad in hand actually has.
        H.Check("fn reads as L4/R4 on an extra-button pad",
                TriggerModes.Describe(TriggerModes.FnButtons, ControllerKind.ExtraButtonPad) == "L4/R4");
        H.Check("fn still reads as Fn1/Fn2 on the Edge",
                TriggerModes.Describe(TriggerModes.FnButtons, ControllerKind.DualSenseEdge) == "Fn1/Fn2");
    }
}
