using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace ControllerWheel;

/// <summary>
/// The update prompt: the new major.minor version, a release-notes link, and Update Now / Later,
/// plus Skip This Version once the user has pressed Later on this version before. Update Now runs the download → verify → silent-install flow inline (progress
/// bar + cancel); on success the app exits through its normal shutdown path and the installer
/// relaunches it. Only ever opened by a user action (the balloon click or the Settings button), so
/// it can never interrupt gameplay on its own.
///
/// Built in code, not XAML, on purpose — no window-level resource dictionary, so it is structurally
/// immune to the StaticResource-at-parse crash class (docs/SETTINGS-UI.md); app-level brushes are
/// fetched with TryFindResource and fall back to literals.
/// </summary>
public sealed class UpdateWindow : Window
{
    private readonly UpdateFeed _feed;
    private readonly StackPanel _buttons;
    private readonly Button _updateNow, _later, _skip;
    private readonly ProgressBar _progress;
    private readonly TextBlock _status;
    private readonly Button _cancel;
    private readonly CheckBox _restartSteam;
    private CancellationTokenSource? _cts;
    private bool _downloading;
    private readonly DispatcherTimer _steamOfferTimer;

    public UpdateWindow(UpdateFeed feed)
    {
        LocWpf.ApplyTo(this);
        _feed = feed;
        Title = Loc.T(UiText.Dialogs.UpdateTitle);
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;   // opened from the tray — there may be no owner window
        Background = System.Windows.Application.Current?.TryFindResource("UiWindowBg") as Brush
                     ?? new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF6));

        var ink = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x38));
        var subInk = new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x66));

        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        root.Children.Add(new TextBlock
        {
            Text = Loc.F(UiText.Dialogs.UpdateHeading, UpdateFeed.ShortVersion(feed.Version)),
            FontSize = 17, FontWeight = FontWeights.Bold, Foreground = ink,
            Margin = new Thickness(0, 0, 0, 10),
        });

        if (feed.NotesUrl is not null)
        {
            var notes = new TextBlock { Margin = new Thickness(0, 0, 0, 12) };
            var link = new System.Windows.Documents.Hyperlink(
                new System.Windows.Documents.Run(Loc.T(UiText.Dialogs.UpdateWhatsNew)))
            { NavigateUri = new Uri(feed.NotesUrl) };
            link.RequestNavigate += (_, e) =>
            {
                try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                catch (Exception ex) { Trace.WriteLine($"[Update] notes link failed: {ex.Message}"); }
            };
            notes.Inlines.Add(link);
            root.Children.Add(notes);
        }

        // ── Download progress (hidden until Update Now) ──
        _status = new TextBlock
        {
            FontSize = 12.5, Foreground = subInk, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed,
        };
        root.Children.Add(_status);
        _progress = new ProgressBar
        {
            Height = 14, Minimum = 0, Maximum = 1, Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 10),
        };
        root.Children.Add(_progress);

        // Steam keeps a pre-cloak pad handle across the update's swap window; the opt-in bounces it once
        // the NEW instance's cloak is up. This process dies before then, so the request is a marker file
        // (SteamRestartFlag) rather than state — armed in RunUpdateAsync, consumed at the next startup.
        _restartSteam = new CheckBox
        {
            IsChecked = true,
            Visibility = SteamSentry.SteamRunning() ? Visibility.Visible : Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 12),
            Content = new TextBlock
            {
                Text = Loc.T(UiText.Dialogs.UpdateRestartSteam),
                FontSize = 12.5, Foreground = subInk, TextWrapping = TextWrapping.Wrap,
            },
        };
        root.Children.Add(_restartSteam);

        _buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _skip = MakeButton(Loc.T(UiText.Dialogs.UpdateSkip), 120);
        _skip.Click += (_, _) => { UpdateService.SkipVersion(_feed.Version); Close(); };
        // Skip is offered only on a repeat prompt: the user must have pressed Later on this version before.
        _skip.Visibility = UpdateService.WasDeferred(feed.Version) ? Visibility.Visible : Visibility.Collapsed;
        _later = MakeButton(Loc.T(UiText.Dialogs.UpdateLater), 80);
        _later.IsCancel = true;
        _later.Click += (_, _) => { UpdateService.DeferVersion(_feed.Version); Close(); };
        _updateNow = MakeButton(Loc.T(UiText.Dialogs.UpdateNow), 110);
        _updateNow.IsDefault = true;
        _updateNow.FontWeight = FontWeights.SemiBold;
        _updateNow.Click += async (_, _) => await RunUpdateAsync();
        _buttons.Children.Add(_skip);
        _buttons.Children.Add(_later);
        _buttons.Children.Add(_updateNow);
        root.Children.Add(_buttons);

        _cancel = MakeButton(Loc.T(UiText.Common.Cancel), 90);
        _cancel.Margin = new Thickness(0);
        _cancel.HorizontalAlignment = HorizontalAlignment.Right;
        _cancel.Visibility = Visibility.Collapsed;
        _cancel.Click += (_, _) => _cts?.Cancel();
        root.Children.Add(_cancel);

        Content = root;
        // Match onboarding: presence changes the offer, never the user's checked choice. Stop polling
        // after acceptance so Steam appearing during a download cannot silently grant restart consent.
        _steamOfferTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _steamOfferTimer.Tick += (_, _) => RefreshSteamRestartOffer(SteamSentry.SteamRunning());
        Loaded += (_, _) => _steamOfferTimer.Start();
        Closed += (_, _) => _steamOfferTimer.Stop();
        Closing += (_, e) => { if (_downloading) _cts?.Cancel(); };
    }

    private static Button MakeButton(string text, double minWidth) => new()
    {
        Content = text, MinWidth = minWidth,
        Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0),
    };

    private void RefreshSteamRestartOffer(bool steamRunning)
    {
        _restartSteam.Visibility = steamRunning ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool AcceptSteamRestartChoice(bool steamRunning)
    {
        // Snapshot only a choice already displayed when Update Now was pressed. A fresh process
        // check may revoke an obsolete offer, but must not introduce an unseen checked offer.
        bool consent = _restartSteam.Visibility == Visibility.Visible
                       && _restartSteam.IsChecked == true && steamRunning;
        _steamOfferTimer.Stop();
        _restartSteam.IsEnabled = false;
        return consent;
    }

    private void ResumeSteamRestartOffer()
    {
        _restartSteam.IsEnabled = true;
        RefreshSteamRestartOffer(SteamSentry.SteamRunning());
        if (IsLoaded) _steamOfferTimer.Start();
    }

    private async System.Threading.Tasks.Task RunUpdateAsync()
    {
        if (_downloading) return;
        bool restartSteamAccepted = AcceptSteamRestartChoice(SteamSentry.SteamRunning());
        _downloading = true;
        _buttons.Visibility = Visibility.Collapsed;
        _cancel.Visibility = Visibility.Visible;
        _progress.Visibility = Visibility.Visible;
        _progress.IsIndeterminate = true;   // until the first sized progress report arrives
        _status.Visibility = Visibility.Visible;
        _status.Text = Loc.F(UiText.Dialogs.UpdateDownloading, UpdateFeed.ShortVersion(_feed.Version));

        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(p =>
        {
            _progress.IsIndeterminate = false;
            _progress.Value = Math.Clamp(p, 0, 1);
        });

        var (path, error) = await UpdateService.DownloadAsync(_feed, progress, _cts.Token);

        _downloading = false;
        _cts.Dispose();
        _cts = null;

        if (path is null)
        {
            // Includes cancel and the hash-mismatch hard reject (the file is already deleted).
            ResumeSteamRestartOffer();
            _status.Text = error ?? Loc.T(UiText.Dialogs.UpdateDownloadFailed);
            _progress.Visibility = Visibility.Collapsed;
            _cancel.Visibility = Visibility.Collapsed;
            _buttons.Visibility = Visibility.Visible;
            return;
        }

        _status.Text = Loc.T(UiText.Dialogs.UpdateInstalling);
        _cancel.Visibility = Visibility.Collapsed;
        // Armed only now that the install is actually happening — a ticked box on a failed download
        // must not leave a marker that bounces Steam on some unrelated future launch.
        if (restartSteamAccepted)
            SteamRestartFlag.Arm();
        if (!UpdateService.LaunchInstallerAndExit(path))
        {
            ResumeSteamRestartOffer();
            _status.Text = Loc.F(UiText.Dialogs.UpdateInstallerFailed, path);
            _progress.Visibility = Visibility.Collapsed;
            _buttons.Visibility = Visibility.Visible;
        }
    }
}
