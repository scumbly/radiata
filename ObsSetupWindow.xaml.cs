using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ControllerWheel;

/// <summary>The OBS connection setup pane — Settings ▸ Advanced ▸ Integrations ▸ "Configure OBS
/// Integration…", and the same window an OBS slice's editor offers while the connection is unset. Port +
/// password for OBS's WebSocket server, behind the Discord pane's one-action-button flow: "Test" until the
/// connection answers, then "Save" (editing a field reverts it), so an untested connection can't be saved.
/// <para>This window does NOT persist anything itself: the values live in <see cref="SystemConfig"/>
/// (ObsPort / ObsPassword / ObsConfigured), which only <see cref="SystemEditorControl"/> writes, via its
/// ApplyTo. On DialogResult = true the caller reads <see cref="ResultPort"/> and
/// <see cref="ResultStoredPassword"/> (already DPAPI-wrapped, and left as the existing blob when the user
/// didn't retype the password, so a save doesn't churn the config with a fresh encryption).</para></summary>
public partial class ObsSetupWindow : Window
{
    private readonly string? _storedPassword;   // DPAPI blob as loaded; kept when the field is untouched
    private readonly SetupDialogFlow _flow;
    private bool _pwDirty;

    public int     ResultPort { get; private set; }
    public string? ResultStoredPassword { get; private set; }

    public ObsSetupWindow(int port, string? storedPassword)
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        _flow = new SetupDialogFlow(ActionBtn);
        _storedPassword = storedPassword;
        PortBox.Text = port.ToString();
        PasswordHint.Visibility = string.IsNullOrEmpty(storedPassword) ? Visibility.Collapsed : Visibility.Visible;
        // The instructions are copy-worthy (the OBS menu path) — selectable, fail-soft.
        Loaded += (_, _) => SelectableText.EnableWithin(this);
    }

    // Editing either field invalidates a prior Test result — the button reverts to "Test".
    private void Field_Changed(object sender, TextChangedEventArgs e) => RevertToTest();   // Port (TextBox)

    private void Secret_Changed(object sender, RoutedEventArgs e)   // Password (PasswordBox)
    {
        _pwDirty = true;
        PasswordHint.Visibility = Visibility.Collapsed;
        RevertToTest();
    }

    private void RevertToTest() => _flow.RevertToTest(StatusText, clearStatusRegardless: true);

    private int Port() => int.TryParse(PortBox.Text, out int p) && p is >= 1 and <= 65535 ? p : 4455;

    /// <summary>The single action button. Before a passing check it reads "Test" and probes OBS; once the
    /// connection answers it becomes "Save" and this click hands the values back.</summary>
    private async void ActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_flow.ShouldSave()) { Save(); return; }

        ActionBtn.IsEnabled = false;
        StatusText.Foreground = System.Windows.Media.Brushes.Gray;
        StatusText.Text = Loc.T(UiText.Wizards.ObsTesting);
        try
        {
            // Untouched password box → test with the STORED secret (the live one the client would use).
            string? pw = _pwDirty
                ? (string.IsNullOrEmpty(PasswordField.Password) ? null : PasswordField.Password)
                : (string.IsNullOrEmpty(_storedPassword) ? null : LocalSecret.Unprotect(_storedPassword));
            string? error = await ObsClient.TestAsync(Port(), pw);
            if (error is null)
            {
                _flow.MarkVerified();
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                StatusText.Text = "✅ " + Loc.T(UiText.Wizards.ObsVerified);
            }
            else
            {
                StatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
                StatusText.Text = "❌ " + error;
            }
        }
        catch (Exception ex)   // async void — contain
        {
            Trace.WriteLine($"[OBS] setup test failed: {ex.Message}");
            StatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
            StatusText.Text = Loc.T(UiText.Wizards.ObsUnreachable);
        }
        finally { ActionBtn.IsEnabled = true; }
    }

    /// <summary>Hand the values back to the caller. Saving marks the connection configured, which is what
    /// releases OBS slices from the configure-on-fire flow.</summary>
    private void Save()
    {
        ResultPort = Port();
        ResultStoredPassword = _pwDirty
            ? (string.IsNullOrEmpty(PasswordField.Password) ? null : LocalSecret.Protect(PasswordField.Password))
            : _storedPassword;
        DialogResult = true;
    }
}
