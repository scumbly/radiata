using System.Windows;
using System.Windows.Controls;

namespace ControllerWheel;

/// <summary>Minimal progress window for <c>Radiata.exe --install-drivers</c> (the installer's optional
/// elevated post-install task). Plain text status lines plus a close-on-done button, built entirely in
/// code with system-default styles — this runs before any config exists and must never depend on
/// Settings resources (the runtime resource-miss crash class).</summary>
internal sealed class DriverInstallWindow : Window
{
    private readonly StackPanel _lines = new();
    private readonly Button _close;
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DriverInstallWindow()
    {
        LocWpf.ApplyTo(this);
        Title = Loc.T(UiText.Dialogs.DriverTitle);
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;

        AddLine(Loc.T(UiText.Dialogs.DriverSettingUp));
        AddLine(Loc.T(UiText.Dialogs.DriverApprove));

        _close = new Button
        {
            Content = Loc.T(UiText.Common.Close), Width = 100, Height = 28, IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0),
        };
        _close.Click += (_, _) => Close();

        var panel = new StackPanel { Margin = new Thickness(18), MinWidth = 380 };
        panel.Children.Add(_lines);
        panel.Children.Add(_close);
        Content = panel;

        Closed += (_, _) => _closed.TrySetResult();
    }

    public void AddLine(string text) =>
        _lines.Children.Add(new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 440, Margin = new Thickness(0, 2, 0, 0),
        });

    /// <summary>Append the engine's result log and unlock the Close button.</summary>
    public void ShowResult(IEnumerable<string> log, bool ok)
    {
        AddLine("");
        foreach (var line in log) AddLine("• " + line);
        AddLine("");
        AddLine(Loc.T(ok ? UiText.Dialogs.DriverDone
                         : UiText.Dialogs.DriverFailed));
        _close.IsEnabled = true;
    }

    /// <summary>Completes when the user (or the system) closes the window.</summary>
    public Task WaitForCloseAsync() => _closed.Task;
}
