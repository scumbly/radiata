using System.Windows;
using WpfColor = System.Windows.Media.Color;

namespace ControllerWheel;

/// <summary>Modal wrapper around <see cref="IconColorPickerControl"/> for the Color Defaults window: the
/// shared editor plus OK/Cancel. (The per-slice icon/color editor hosts the same control INLINE — no
/// dialog — sharing the slice editor's Save Slice button.)</summary>
public partial class DefaultPickerWindow : Window
{
    public WpfColor SelectedColor => Picker.SelectedColor;
    public string?  SelectedIcon  => Picker.SelectedIcon;

    /// <summary>True when the user clicked "Fetch Logo" (installed-game slices; shown via
    /// <paramref name="showFetchLogo"/>). The dialog closes as OK; the HOST does the actual fetch.</summary>
    public bool FetchLogoRequested { get; private set; }

    public DefaultPickerWindow(string name, WpfColor color, string? icon, WpfColor defColor, string? defIcon,
                               bool showFetchLogo = false)
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        NameText.Text = name;
        Picker.Configure(color, icon, defColor, defIcon, showFetchLogo);
        Picker.FetchLogoClicked += (_, _) => { FetchLogoRequested = true; DialogResult = true; };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
