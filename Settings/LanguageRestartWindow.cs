using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>The language-change prompt: the question in the language being CHOSEN, then in the current language in
/// italics, with Yes/No carrying both words ("Sí / Yes"). A MessageBox cannot do this — its buttons follow Windows'
/// language, not the app's — and a user who just picked a language they can read needs the question in it.</summary>
internal sealed class LanguageRestartWindow : Window
{
    /// <summary>True when the user chose to restart now.</summary>
    public static bool Ask(Window owner, string code)
    {
        var w = new LanguageRestartWindow(code) { Owner = owner };
        return w.ShowDialog() == true;
    }

    private LanguageRestartWindow(string code)
    {
        var chosen = HelpLocalization.Language(code);
        Title = Both(code, UiText.Settings.LanguageCaption);
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        FlowDirection = LocWpf.Flow;   // the window reads in the CURRENT language; the first line carries its own direction
        Background = System.Windows.Application.Current.TryFindResource("UiWindowBg") as Brush ?? Brushes.White;

        var chosenLine = new TextBlock
        {
            Text = Loc.In(code, UiText.Settings.LanguageRestartPrompt),
            FontFamily = new FontFamily(chosen.FontFamily), FontSize = 14, TextWrapping = TextWrapping.Wrap, MaxWidth = 400,
            FlowDirection = chosen.Rtl ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight,
        };
        var currentLine = new TextBlock
        {
            Text = Loc.T(UiText.Settings.LanguageRestartPrompt),
            FontStyle = FontStyles.Italic, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxWidth = 400,
            Foreground = System.Windows.Application.Current.TryFindResource("UiMutedInk") as Brush ?? Brushes.Gray,
            Margin = new Thickness(0, 6, 0, 0),
        };
        var yes = new Button { Content = Both(code, UiText.Settings.Yes), MinWidth = 96, Padding = new Thickness(14, 5, 14, 5), IsDefault = true };
        var no  = new Button { Content = Both(code, UiText.Settings.No),  MinWidth = 96, Padding = new Thickness(14, 5, 14, 5), IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        yes.Click += (_, _) => { DialogResult = true; Close(); };
        no.Click  += (_, _) => { DialogResult = false; Close(); };
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(yes); buttons.Children.Add(no);

        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 16) };
        root.Children.Add(chosenLine); root.Children.Add(currentLine); root.Children.Add(buttons);
        Content = root;
    }

    /// <summary>"Chosen / current" — collapsed to one word when both languages spell it the same.</summary>
    private static string Both(string code, string english)
    {
        string a = Loc.In(code, english), b = Loc.T(english);
        return a == b ? a : $"{a} / {b}";
    }
}
