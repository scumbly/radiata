using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CheckBox = System.Windows.Controls.CheckBox;   // Button is aliased app-wide in GlobalUsings

namespace ControllerWheel;

/// <summary>The dialog shown by <c>Radiata.exe --uninstall</c> (launched from the
/// "Uninstall Radiata.cmd" that ships in the app folder) and, with <c>cleanupOnly</c>, by
/// <c>--uninstall-cleanup</c> (the Inno uninstaller's pre-delete hook). Both offer the same two
/// OFF-by-default opt-ins: remove the SHARED ViGEmBus/HidHide drivers (other tools use them) and
/// delete the user's settings.
/// <para>The two modes differ in KIND: the portable dialog is a genuine confirmation (Cancel/close
/// returns null and nothing happens), while the cleanup dialog is an OPTIONS prompt — Inno cannot
/// abort an uninstall once [UninstallRun] fires, so the files go away regardless and a Cancel here
/// would be a lie that strands the Run key, cloak, and recovery task. It has a single Continue
/// button, never returns null, and closing the window means Continue with no opt-ins.</para></summary>
internal static class UninstallDialog
{
    public static (bool Drivers, bool AppData)? Prompt(bool cleanupOnly = false)
    {
        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), MaxWidth = 420,
            Text = cleanupOnly
                ? Loc.T(UiText.Dialogs.Uninstalling)
                : Loc.T(UiText.Dialogs.UninstallBody),
        };

        var drivers = new CheckBox
        {
            Content = Loc.T(UiText.Dialogs.UninstallDrivers), Margin = new Thickness(0, 2, 0, 0),
        };
        var driversNote = new TextBlock
        {
            Text = Loc.T(UiText.Dialogs.UninstallDriversNote),
            Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 0, 0, 8), MaxWidth = 400,
        };
        var appData = new CheckBox
        {
            Content = Loc.T(UiText.Dialogs.UninstallSettings), Margin = new Thickness(0, 2, 0, 0),
        };

        var ok = new Button
        {
            Content = Loc.T(cleanupOnly ? UiText.Dialogs.UninstallContinue : UiText.Dialogs.UninstallGo),
            Width = 100, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true,
        };
        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0),
        };
        buttons.Children.Add(ok);
        if (!cleanupOnly)   // cleanup mode has no Cancel — the uninstall proceeds either way
            buttons.Children.Add(new Button { Content = Loc.T(UiText.Common.Cancel), Width = 100, Height = 28, IsCancel = true });

        var panel = new StackPanel { Margin = new Thickness(18) };
        // Local-only opt-in (your settings) reads first; the system-wide one other apps depend on
        // reads last, immediately above its own caveat — driversNote is a panel sibling, not a child
        // of the checkbox, so it must travel with `drivers`.
        panel.Children.Add(body);
        panel.Children.Add(appData);
        panel.Children.Add(drivers);
        panel.Children.Add(driversNote);
        panel.Children.Add(buttons);

        var win = new Window
        {
            FlowDirection = LocWpf.Flow,
            Title = Loc.T(UiText.Dialogs.UninstallTitle),
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = true,
            Content = panel,
        };
        ok.Click += (_, _) => win.DialogResult = true;

        if (win.ShowDialog() == true) return (drivers.IsChecked == true, appData.IsChecked == true);
        // Cleanup mode never cancels: closing the window is Continue with no opt-ins (the Inno
        // uninstall proceeds regardless, and skipping base cleanup would strand the Windows state).
        return cleanupOnly ? (false, false) : null;
    }
}
