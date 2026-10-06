using System.Linq;
using System.Windows;
using System.Windows.Controls;
// WinForms is referenced app-wide (tray icon) — pin the WPF/Win32 types explicitly.
using CheckBox = System.Windows.Controls.CheckBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ControllerWheel;

/// <summary>The Settings "Exceptions" tab: the global Anticheat Passthru Mode switch (persists
/// <see cref="SystemConfig.CaptureSafeMode"/> — the same setting the tray item toggles) plus the
/// automatic-exceptions list (<see cref="SystemConfig.SafeModeApps"/>): apps that engage passthru mode while
/// they're running (or frontmost, per entry), matched HidHide-style by full exe path. The App-side
/// watcher (ConfigureSafeModeWatcher / PollSafeModeApps) does the runtime work; this tab only edits
/// config, via the same Load/ApplyTo contract as the other tabs.</summary>
public partial class ExceptionsEditorControl : UserControl
{
    public event EventHandler? Changed;

    /// <summary>The tab-level Help chip — SettingsWindow jumps to the Help topic.</summary>
    public event Action<string>? HelpRequested;
    private void LearnMore_Click(object sender, RoutedEventArgs e) => HelpRequested?.Invoke("passthru-mode");

    private bool _loading;
    private readonly List<SafeModeApp> _apps = new();
    private bool _gamesLoaded;

    /// <summary>One installed-games dropdown row (a real game, or the non-selectable prompt at index 0).</summary>
    private sealed record GamePick(string Display, InstalledGame? Game);

    public ExceptionsEditorControl() => InitializeComponent();

    public void Load(SystemConfig cfg)
    {
        _loading = true;
        SafeModeBox.IsChecked = cfg.CaptureSafeMode;
        _apps.Clear();
        _apps.AddRange(cfg.SafeModeApps);
        RebuildList();
        _loading = false;
        LoadGamesDropdown();   // idempotent — scans installed games once, off-thread
    }

    // ── Installed-games dropdown ─────────────────────────────────────────────────

    private async void LoadGamesDropdown()
    {
        if (_gamesLoaded) return;
        _gamesLoaded = true;
        SetGamePrompt(Loc.T(UiText.Passthru.ScanningGames), enabled: false);
        try
        {
            // Only games with a resolvable install directory can be folder-matched; sorted by name,
            // deduped by directory (two storefronts can report the same install).
            var games = await System.Threading.Tasks.Task.Run(() => GameLibrary.Scan()
                .Where(g => !string.IsNullOrWhiteSpace(g.InstallDir) && System.IO.Directory.Exists(g.InstallDir))
                .GroupBy(g => g.InstallDir!, StringComparer.OrdinalIgnoreCase)
                .Select(grp => grp.First())
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());

            var items = new List<GamePick>
            {
                new(Loc.T(games.Count == 0 ? UiText.Passthru.NoGamesFound : UiText.Passthru.AddInstalledGame), null),
            };
            items.AddRange(games.Select(g => new GamePick(g.DisplayName, g)));

            _loading = true;
            AddGameBox.ItemsSource = items;
            AddGameBox.SelectedIndex = 0;
            AddGameBox.IsEnabled = games.Count > 0;
            _loading = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Exceptions] game scan failed: {ex.Message}");
            SetGamePrompt(Loc.T(UiText.Passthru.ScanFailed), enabled: false);
        }
    }

    private void SetGamePrompt(string text, bool enabled)
    {
        _loading = true;
        AddGameBox.ItemsSource = new List<GamePick> { new(text, null) };
        AddGameBox.SelectedIndex = 0;
        AddGameBox.IsEnabled = enabled;
        _loading = false;
    }

    private void AddGameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AddGameBox.SelectedItem is not GamePick { Game: { } game }) return;

        // Add as a FOLDER entry (match anything running from the game's install dir). Skip a dup.
        var dir = game.InstallDir!;
        if (!_apps.Any(a => a.MatchFolder && string.Equals(a.Path, dir, StringComparison.OrdinalIgnoreCase)))
        {
            _apps.Add(new SafeModeApp { Path = dir, MatchFolder = true, Name = game.Name });
            RebuildList();
            OnChanged();
        }
        AddGameBox.SelectedIndex = 0;   // reset to the prompt (it's an "add" selector, not a state)
    }

    /// <summary>Fold this tab's fields into <paramref name="cfg"/> (a `with` copy — untouched fields pass
    /// through, same contract as the Customize/Advanced tabs).</summary>
    public SystemConfig ApplyTo(SystemConfig cfg) => cfg with
    {
        CaptureSafeMode = SafeModeBox.IsChecked == true,
        SafeModeApps    = new List<SafeModeApp>(_apps),
    };

    // ── Exceptions list ─────────────────────────────────────────────────────────

    private void RebuildList()
    {
        AppsList.Items.Clear();
        for (int i = 0; i < _apps.Count; i++) AppsList.Items.Add(BuildRow(_apps[i], i));
    }

    private UIElement BuildRow(SafeModeApp app, int idx)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Title: the game name (folder entries carry one) or the exe file name. Folder entries get a
        // small "install folder" tag so the two match modes read differently at a glance.
        var title = app.Name
                    ?? (app.MatchFolder ? new System.IO.DirectoryInfo(app.Path).Name
                                        : System.IO.Path.GetFileName(app.Path));
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (app.MatchFolder)
            titleRow.Children.Add(new TextBlock
            {
                Text = Loc.T(UiText.Passthru.InstallFolder),
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 1, 0, 0),
            });
        text.Children.Add(titleRow);
        text.Children.Add(new TextBlock
        {
            Text = app.Path,
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Gray,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = app.Path,
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var front = new ComboBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            MinWidth = 137,   // a floor, never a fixed Width: the longer translations must be able to grow it
            ToolTip = Loc.T(UiText.Passthru.WhenEngages),
        };
        front.Items.Add(new ComboBoxItem { Content = Loc.T(UiText.Passthru.WhileRunning) });
        front.Items.Add(new ComboBoxItem { Content = Loc.T(UiText.Passthru.WhileFrontmost) });
        front.SelectedIndex = app.FrontmostOnly ? 1 : 0;
        // Records are immutable — replace the entry at this row's index (indices stay valid: a mode
        // change doesn't rebuild the list; add/remove do).
        front.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            _apps[idx] = _apps[idx] with { FrontmostOnly = front.SelectedIndex == 1 };
            OnChanged();
        };
        Grid.SetColumn(front, 1);
        grid.Children.Add(front);

        var remove = new Button
        {
            Content = "✕",
            FontWeight = FontWeights.Light,   // unicode glyph reads heavy at button size
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Loc.T(UiText.Passthru.RemoveException),
        };
        // Glyph-only button: name it per row so a screen reader says which exception is being removed.
        System.Windows.Automation.AutomationProperties.SetName(remove, Loc.F(UiText.Passthru.RemoveExceptionName, title));
        remove.Click += (_, _) => { _apps.RemoveAt(idx); RebuildList(); OnChanged(); };
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);

        return new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x0A, 0, 0, 0)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 6),
            Child = grid,
        };
    }

    private void AddAppBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.T(UiText.Passthru.AddAppTitle),
            Filter = Loc.T(UiText.Passthru.AppsFilter),
            Multiselect = true,
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        bool added = false;
        foreach (var file in dlg.FileNames)
        {
            if (_apps.Any(a => string.Equals(a.Path, file, StringComparison.OrdinalIgnoreCase))) continue;
            _apps.Add(new SafeModeApp { Path = file });   // FrontmostOnly defaults off (recommended)
            added = true;
        }
        if (!added) return;
        RebuildList();
        OnChanged();
    }

    private void SafeModeBox_Changed(object sender, RoutedEventArgs e) => OnChanged();

    /// <summary>The label beside the (scaled) checkbox toggles it, like normal CheckBox content would.</summary>
    private void SafeModeLabel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        SafeModeBox.IsChecked = SafeModeBox.IsChecked != true;

    private void OnChanged() { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }
}
