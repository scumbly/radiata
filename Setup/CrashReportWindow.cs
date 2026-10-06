using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace ControllerWheel;

/// <summary>
/// The crash-report consent window, shown at a quiet moment on the first startup after a crash
/// (App defers it — it must never appear over a game or the overlay). Shows the FULL report body in
/// a read-only scrollable box — what's on screen is byte-for-byte what Send posts (PRIVACY.md) —
/// with Send / Copy to clipboard / Don't send, and a "Remember this choice" box. Send and Don't send
/// both dismiss the window at once. Send posts from a detached task: success deletes the pending file,
/// failure keeps it (retry next launch). Ticked, Send calls <c>enableAutoSend</c>
/// (<c>CrashAutoSend</c> true) and Don't send deletes the file and calls <c>disablePrompting</c>
/// (<c>CrashPromptEnabled</c> false). Closing via ✕ decides nothing: the file stays, no remembered
/// choice is written, and the offer repeats next start.
///
/// Built in code, not XAML — no window-level resource dictionary, so it is structurally immune to
/// the StaticResource-at-parse crash class (docs/SETTINGS-UI.md).
/// </summary>
public sealed class CrashReportWindow : Window
{
    private readonly string _report;
    private readonly Action _disablePrompting, _enableAutoSend;
    private readonly CheckBox _remember;
    private readonly TextBlock _result;
    private readonly Button _send, _copy, _dontSend;

    public CrashReportWindow(string report, Action disablePrompting, Action enableAutoSend)
    {
        LocWpf.ApplyTo(this);
        _report = report;
        _disablePrompting = disablePrompting;
        _enableAutoSend = enableAutoSend;

        Title = Loc.T(UiText.Dialogs.CrashTitle);
        Width = 560;
        Height = 520;
        MinWidth = 440; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;       // tray app — there may be no owner window
        ShowActivated = false;      // offer, don't grab: never steal focus from whatever the user is doing
        Background = System.Windows.Application.Current?.TryFindResource("UiWindowBg") as Brush
                     ?? new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF6));

        var ink = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x38));
        var subInk = new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x66));

        var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var intro = new TextBlock
        {
            Text = Loc.T(UiText.Dialogs.CrashIntro),
            FontSize = 13, Foreground = ink, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetRow(intro, 0);
        root.Children.Add(intro);

        var box = new TextBox
        {
            Text = report,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11.5,
            AcceptsReturn = true,
        };
        Grid.SetRow(box, 1);
        root.Children.Add(box);

        var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        _result = new TextBlock
        {
            FontSize = 12.5, Foreground = subInk, TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8),
        };
        footer.Children.Add(_result);

        _remember = new CheckBox
        {
            Content = new TextBlock
            {
                Text = Loc.T(UiText.Dialogs.CrashRemember), TextWrapping = TextWrapping.Wrap,
                Foreground = ink, FontSize = 12.5,
            },
            Margin = new Thickness(0, 0, 0, 10),
        };
        footer.Children.Add(_remember);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _dontSend = MakeButton(Loc.T(UiText.Dialogs.CrashDontSend), 100);
        _dontSend.IsCancel = true;   // Esc = the no-consent path, never an accidental send
        _dontSend.Click += (_, _) =>
        {
            CrashReporter.DeletePending();
            ApplyRemembered(_disablePrompting, "disable-prompt");
            Close();
        };
        _copy = MakeButton(Loc.T(UiText.Dialogs.CrashCopy), 130);
        _copy.Click += (_, _) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(_report);
                ShowResult(Loc.T(UiText.Dialogs.CrashCopied));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Crash] clipboard copy failed: {ex.Message}");
                ShowResult(Loc.T(UiText.Dialogs.CrashCopyFailed));
            }
        };
        _send = MakeButton(Loc.T(UiText.Dialogs.CrashSend), 90);
        _send.FontWeight = FontWeights.SemiBold;
        _send.Click += (_, _) => SendAndClose();
        buttons.Children.Add(_dontSend);
        buttons.Children.Add(_copy);
        buttons.Children.Add(_send);
        footer.Children.Add(buttons);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;
    }

    private static Button MakeButton(string text, double minWidth) => new()
    {
        Content = text, MinWidth = minWidth,
        Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0),
    };

    private void ApplyRemembered(Action write, string what)
    {
        if (_remember.IsChecked != true) return;
        try { write(); }
        catch (Exception ex) { Trace.WriteLine($"[Crash] remember-choice ({what}) write failed: {ex.Message}"); }
    }

    private void ShowResult(string text)
    {
        _result.Text = text;
        _result.Visibility = Visibility.Visible;
    }

    /// <summary>Dismisses at once; the post runs detached so the window never waits on the network.
    /// Failure keeps the pending file for the next launch.</summary>
    private void SendAndClose()
    {
        _send.IsEnabled = false; _dontSend.IsEnabled = false;
        var report = _report;
        ApplyRemembered(_enableAutoSend, "auto-send");
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                if (await CrashReporter.SendAsync(report)) CrashReporter.DeletePending();
                else Trace.WriteLine("[Crash] send failed; pending report kept for next launch");
            }
            catch (Exception ex) { Trace.WriteLine($"[Crash] send threw: {ex.Message}"); }
        });
        Close();
    }
}
