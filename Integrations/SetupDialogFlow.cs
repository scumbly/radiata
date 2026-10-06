using System.Windows.Controls;

namespace ControllerWheel;

/// <summary>The one-button Test⇄Save state machine shared by <see cref="ObsSetupWindow"/> and
/// <see cref="DiscordSetupWindow"/>: the button reads "Test" until a check passes, then "Save" (any field
/// edit reverts it to "Test"). Owns only the button's content and the verified flag; each window still
/// runs its own test call, status text and colours.</summary>
internal sealed class SetupDialogFlow
{
    private readonly Button _actionBtn;
    private bool _verified;

    public SetupDialogFlow(Button actionBtn) => _actionBtn = actionBtn;

    /// <summary>True once a Test has passed for the current field values — the caller's ActionBtn_Click
    /// checks this first and calls its own Save() instead of testing again.</summary>
    public bool ShouldSave() => _verified;

    /// <summary>A successful Test: flips the button to "Save" and gates it there until a field changes.</summary>
    public void MarkVerified() => (_verified, _actionBtn.Content) = (true, Loc.T(UiText.Wizards.ActionSave));

    /// <summary>Editing a field invalidates a prior Test result. <paramref name="clearStatusRegardless"/>
    /// captures each window's own existing behaviour: OBS clears the status line on every edit, Discord
    /// only when a previous Test had passed.</summary>
    public void RevertToTest(TextBlock statusText, bool clearStatusRegardless)
    {
        if (clearStatusRegardless) statusText.Text = "";
        if (!_verified) return;
        _verified = false;
        _actionBtn.Content = Loc.T(UiText.Wizards.ActionTest);
        if (!clearStatusRegardless) statusText.Text = "";
    }
}
