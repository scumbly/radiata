using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>RADIATA PICKER (dev-only; removal map in <see cref="RadiataPicker"/>): a simple MOUSE-driven
/// window for choosing the LOGO each of the top-100 games' WHEEL SLICE should default to. Left: the game
/// list (✓ = picked). Right: the game's SGDB logo candidates (the exact set slice ◀▶ cycling draws from),
/// previewed on dark like a slice renders; the first tile is the current AUTOMATIC slice resolution
/// (aspect-band pick) — clicking it clears the explicit choice. Picks persist as portable URL-keyed JSON
/// in %APPDATA%\Radiata\picker-slice-logos.json — that file IS the export for pre-baking.</summary>
public partial class SliceLogoPickerWindow : Window
{
    private static readonly string PicksPath = Path.Combine(AppPaths.AppDataDir, "picker-slice-logos.json");

    private sealed class Pick
    {
        public string Name  { get; set; } = "";
        public string Url   { get; set; } = "";
        public int    Index { get; set; }
    }

    private readonly IReadOnlyList<InstalledGame> _games = RadiataPicker.SeedGames();
    private Dictionary<string, Pick> _picks = LoadPicks();   // key = GameLibrary.NormalizeName
    private int _loadGen;                                    // stale-async guard (fast game switching)

    public SliceLogoPickerWindow()
    {
        InitializeComponent();
        FillList(keepIndex: 0);
    }

    private static Dictionary<string, Pick> LoadPicks()
    {
        try
        {
            return File.Exists(PicksPath)
                ? JsonSerializer.Deserialize<Dictionary<string, Pick>>(File.ReadAllText(PicksPath)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    private void SavePicks()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDir);
            File.WriteAllText(PicksPath, JsonSerializer.Serialize(_picks, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { StatusText.Text = $"save failed: {ex.Message}"; }
    }

    private void FillList(int keepIndex)
    {
        GamesList.SelectionChanged -= GamesList_SelectionChanged;
        GamesList.Items.Clear();
        foreach (var g in _games)
            GamesList.Items.Add((_picks.ContainsKey(GameLibrary.NormalizeName(g.Name)) ? "✓  " : "    ") + g.Name);
        GamesList.SelectionChanged += GamesList_SelectionChanged;
        GamesList.SelectedIndex = Math.Clamp(keepIndex, 0, _games.Count - 1);
    }

    private InstalledGame? SelectedGame =>
        GamesList.SelectedIndex is var i && i >= 0 && i < _games.Count ? _games[i] : null;

    private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedGame is { } g) _ = LoadCandidatesAsync(g, refresh: false);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGame is not { } g) return;
        GameArt.RetryMissedArt();   // clear .miss markers so a rate-limited/failed fetch re-attempts
        _ = LoadCandidatesAsync(g, refresh: true);
    }

    private async Task LoadCandidatesAsync(InstalledGame game, bool refresh)
    {
        int gen = ++_loadGen;
        CandidatePanel.Children.Clear();
        HeaderText.Text = game.Name;
        StatusText.Text = "loading…";
        _picks.TryGetValue(GameLibrary.NormalizeName(game.Name), out var current);

        // Tile 0: the automatic slice resolution (GetSliceLogoPathAsync's aspect-band pick) — what a
        // slice shows today with no explicit choice. Clicking it CLEARS the pick.
        string? auto = null;
        try { auto = await GameArt.GetSliceLogoPathAsync(game); } catch { }
        if (gen != _loadGen) return;
        AddTile(game, auto, url: null, index: -1, label: "Automatic (slice default)", selected: current is null);

        IReadOnlyList<string> urls;
        try { urls = await GameArt.GetLogoCandidateUrlsAsync(game, refresh); } catch { urls = []; }
        if (gen != _loadGen) return;

        int shown = 0;
        for (int i = 0; i < urls.Count; i++)
        {
            string? path = null;
            try { path = await GameArt.DownloadLogoCandidateAsync(game, urls[i], i); } catch { }
            if (gen != _loadGen) return;
            if (path is null) continue;
            AddTile(game, path, urls[i], i, $"candidate {i + 1}",
                    selected: current is not null && string.Equals(current.Url, urls[i], StringComparison.OrdinalIgnoreCase));
            shown++;
        }
        StatusText.Text = shown == 0
            ? "no SteamGridDB logo candidates (missing key, no art, or rate-limited — try Refresh art)"
            : $"{shown} candidate(s)" + (current is null ? "" : "  ·  picked ✓");
    }

    private void AddTile(InstalledGame game, string? imagePath, string? url, int index, string label, bool selected)
    {
        ImageSource? img = imagePath is null ? null : GameArt.LoadFromFile(imagePath);
        var stack = new StackPanel { Width = 200 };
        stack.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x20)),   // slice-dark preview bed
            CornerRadius = new CornerRadius(4),
            Height = 84,
            Padding = new Thickness(8),
            Child = img is null
                ? new TextBlock { Text = "(no image)", Foreground = Brushes.Gray,
                                  HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new Image { Source = img, Stretch = Stretch.Uniform },
        });
        stack.Children.Add(new TextBlock
        {
            Text = label, FontSize = 11, Margin = new Thickness(0, 3, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xC2)),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var btn = new Button
        {
            Content = stack,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(2),
            BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(0x4E, 0xA1, 0xFF)) : Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = url ?? "Automatic pick (no saved choice)",
        };
        btn.Click += (_, _) =>
        {
            string norm = GameLibrary.NormalizeName(game.Name);
            if (url is null) _picks.Remove(norm);
            else _picks[norm] = new Pick { Name = game.Name, Url = url, Index = index };
            SavePicks();
            FillList(keepIndex: GamesList.SelectedIndex);   // re-check ✓; re-triggers candidate reload
        };
        CandidatePanel.Children.Add(btn);
    }
}
