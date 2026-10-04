using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;

namespace ControllerWheel;

/// <summary>
/// The stern first-discovery gate for drop-in packages: shown once per new (or content-changed)
/// package, before anything from it is loaded. Deliberately requires an ACTIVE confirmation — the
/// accept button stays disabled until the responsibility checkbox is ticked — and Decline is the
/// default/Enter/Esc action, so a reflex click can never consent.
///
/// Built in code, not XAML, on purpose: it uses no window-level resource dictionary at all, so it
/// is structurally immune to the StaticResource-at-parse crash class (docs/SETTINGS-UI.md); the one
/// app-level brush is fetched with TryFindResource and falls back to a literal.
/// </summary>
public sealed class PackageConsentWindow : Window
{
    private readonly CheckBox _acknowledge;
    private readonly Button _accept;

    /// <summary>True only when the user ticked the box and clicked the accept button.</summary>
    public bool Accepted { get; private set; }

    /// <param name="runsCode">True for arcade game packages — the package contains a SCRIPT that
    /// will execute (sandboxed), so the warning and the responsibility line get sterner, code-worded
    /// copy. Materials stay on the data-only wording.</param>
    public PackageConsentWindow(string kindLabel, string displayName, string author,
                                string folderName, string contentHash, bool runsCode = false)
    {
        LocWpf.ApplyTo(this);
        Title = Loc.T(UiText.Dialogs.PackageTitle);
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;               // may appear with no owner window (tray app)
        Topmost = true;                     // a consent gate must not be lost behind a game
        Background = Application.Current?.TryFindResource("UiWindowBg") as Brush
                     ?? new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF6));

        var ink = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x38));
        var subInk = new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x66));

        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        root.Children.Add(new TextBlock
        {
            Text = Loc.F(UiText.Dialogs.PackageApproval, kindLabel),
            FontSize = 17, FontWeight = FontWeights.Bold, Foreground = ink,
            Margin = new Thickness(0, 0, 0, 10),
        });

        var facts = new TextBlock
        {
            Foreground = subInk, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };
        facts.Inlines.Add(new System.Windows.Documents.Run(Loc.T(UiText.Dialogs.PackageName)) { FontWeight = FontWeights.SemiBold });
        facts.Inlines.Add(displayName + "\n");
        facts.Inlines.Add(new System.Windows.Documents.Run(Loc.T(UiText.Dialogs.PackageAuthor)) { FontWeight = FontWeights.SemiBold });
        facts.Inlines.Add(author + "\n");
        facts.Inlines.Add(new System.Windows.Documents.Run(Loc.T(UiText.Dialogs.PackageFolder)) { FontWeight = FontWeights.SemiBold });
        facts.Inlines.Add((runsCode ? "Packages\\Arcade Games\\" : "Packages\\Materials\\") + folderName + "\n");
        facts.Inlines.Add(new System.Windows.Documents.Run(Loc.T(UiText.Dialogs.PackageFingerprint)) { FontWeight = FontWeights.SemiBold });
        facts.Inlines.Add(contentHash.Length >= 16 ? contentHash[..16].ToLowerInvariant() : contentHash);
        root.Children.Add(facts);

        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0xC0, 0x50, 0x40)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xC0, 0x50, 0x40)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBlock
            {
                Foreground = ink, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
                Text = Loc.T(runsCode ? UiText.Dialogs.PackageCodeWarning : UiText.Dialogs.PackageDataWarning),
            },
        });

        _acknowledge = new CheckBox
        {
            Content = new TextBlock
            {
                Text = Loc.T(runsCode ? UiText.Dialogs.PackageAcknowledgeCode : UiText.Dialogs.PackageAcknowledgeData),
                TextWrapping = TextWrapping.Wrap, Foreground = ink, FontSize = 12.5,
            },
            Margin = new Thickness(0, 0, 0, 14),
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        _acknowledge.Checked += (_, _) => _accept!.IsEnabled = true;
        _acknowledge.Unchecked += (_, _) => _accept!.IsEnabled = false;
        root.Children.Add(_acknowledge);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var decline = new Button
        {
            Content = Loc.T(UiText.Dialogs.PackageDontLoad), MinWidth = 110, Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6), IsDefault = true, IsCancel = true,
        };
        decline.Click += (_, _) => { Accepted = false; DialogResult = false; };
        _accept = new Button
        {
            Content = Loc.T(UiText.Dialogs.PackageLoad), MinWidth = 130,
            Padding = new Thickness(12, 6, 12, 6), IsEnabled = false,
        };
        _accept.Click += (_, _) => { Accepted = _acknowledge.IsChecked == true; DialogResult = Accepted; };
        buttons.Children.Add(decline);
        buttons.Children.Add(_accept);
        root.Children.Add(buttons);

        Content = root;
    }
}
