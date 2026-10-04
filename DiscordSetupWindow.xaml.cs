using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ControllerWheel;

/// <summary>Mini "bring your own credentials" wizard for Discord voice-channel slices — opened on demand
/// from a Join/Leave Voice Channel slice (via the "Configure Discord Integration" button the slice editor
/// shows when <see cref="DiscordOAuth.IsConfigured"/> is false), so users don't have to hunt through
/// Settings ▸ Advanced. Numbered how-to (the "Developer Portal" link opens the portal) + Client ID/Secret
/// fields + a single action button that reads "Test" until the pair validates against Discord, then becomes
/// "Save" (editing a field reverts it to "Test"). Save persists via DiscordOAuth (DPAPI-encrypted; the
/// Settings ▸ Advanced button opens this same window). DialogResult = true once saved.</summary>
public partial class DiscordSetupWindow : Window
{
    private readonly SetupDialogFlow _flow;

    public DiscordSetupWindow()
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        _flow = new SetupDialogFlow(ActionBtn);
        var creds = DiscordOAuth.Load();   // pre-fill if partly/previously configured
        if (creds is { } c) { IdBox.Text = c.ClientId; SecretBox.Password = c.ClientSecret; }
        // The how-to text is copy-worthy (esp. the "http://localhost" redirect) — selectable, fail-soft.
        Loaded += (_, _) => SelectableText.EnableWithin(this);
    }

    /// <summary>The ⧉ copy glyph beside the redirect URL — puts "http://localhost" on the clipboard for
    /// pasting into Discord's OAuth2 redirect field; brief ✓ feedback on the glyph itself.</summary>
    private void CopyRedirect_Click(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText("http://localhost"); } catch { return; }
        CopyRedirectGlyph.Text = "✓";
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        t.Tick += (_, _) => { t.Stop(); CopyRedirectGlyph.Text = "⧉"; };
        t.Start();
    }

    // Editing either field invalidates a prior Test result — the button reverts to "Test".
    private void Field_Changed(object sender, TextChangedEventArgs e) => RevertToTest();   // Client ID (TextBox)
    private void Secret_Changed(object sender, RoutedEventArgs e)     => RevertToTest();   // Client Secret (PasswordBox)
    private void RevertToTest() => _flow.RevertToTest(StatusText, clearStatusRegardless: false);

    /// <summary>The single action button. Before a successful check it reads "Test" and validates; once the
    /// credentials pass it becomes "Save" and this click persists them.</summary>
    private async void ActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_flow.ShouldSave()) { Save(); return; }

        var id = IdBox.Text.Trim();
        var secret = SecretBox.Password.Trim();
        if (id.Length == 0 || secret.Length == 0)
        {
            StatusText.Text = Loc.T(UiText.Wizards.DiscordNeedBoth);
            return;
        }
        ActionBtn.IsEnabled = false;
        StatusText.Text = Loc.T(UiText.Wizards.DiscordChecking);
        try
        {
            if (await DiscordOAuth.TestAsync(id, secret))
            {
                _flow.MarkVerified();
                StatusText.Text = "✅ " + Loc.T(UiText.Wizards.DiscordVerified);
            }
            else
            {
                StatusText.Text = "❌ " + Loc.T(UiText.Wizards.DiscordRejected);
            }
        }
        catch (Exception ex)   // async void — contain
        {
            Trace.WriteLine($"[Discord] slice setup test failed: {ex.Message}");
            StatusText.Text = Loc.T(UiText.Wizards.DiscordCheckFailed);
        }
        finally { ActionBtn.IsEnabled = true; }
    }

    private void Save()
    {
        try
        {
            DiscordOAuth.Save(IdBox.Text.Trim(), SecretBox.Password.Trim());   // DPAPI-encrypted; shared with Settings ▸ Advanced
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Discord] slice setup save failed: {ex.Message}");
            StatusText.Text = Loc.T(UiText.Wizards.DiscordWriteFailed);
        }
    }
}
