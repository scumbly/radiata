using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerWheel;

public partial class IconPickerWindow : Window
{
    public string? SelectedIconName { get; private set; }

    /// <summary>One tile row: the 80px tile plus its 2px margin top and bottom. Pixel scrolling to a row
    /// multiplies by this, so it must match the tile's Height + Margin in the XAML.</summary>
    public const double RowHeight = 84;
    private const double CellWidth = 80;   // tile Width 76 + 2px margin each side

    /// <summary>A tile's data. The glyph is built on first bind — only rows the virtualizing list
    /// realizes ever ask — and cached by <see cref="PackIconHelper"/>.</summary>
    public sealed record IconCell(string Name, bool IsCurrent)
    {
        public ImageSource? Image => PackIconHelper.GetCached(Name);
    }

    private string[] _matches = [];
    private int _columns;
    private bool _scrollToCurrent;

    public IconPickerWindow(string? current = null)
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        SelectedIconName = current;
        _scrollToCurrent = !string.IsNullOrWhiteSpace(current);
        Filter("");
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Filter(SearchBox.Text);

    private void Filter(string filter)
    {
        var f = filter.Trim();
        _matches = f.Length == 0
            ? PackIconHelper.AllNames
            : [.. PackIconHelper.AllNames.Where(n => n.Contains(f, StringComparison.OrdinalIgnoreCase))];
        // Digits stay left-to-right inside an Arabic sentence, so the grouped counts need no extra isolation.
        string total = PackIconHelper.AllNames.Length.ToString("N0", Loc.Culture);
        MatchCount.Text = f.Length == 0
            ? Loc.F(UiText.IconPicker.Count, total)
            : Loc.F(UiText.IconPicker.CountOf, _matches.Length.ToString("N0", Loc.Culture), total);
        RebuildRows();
        Scroller()?.ScrollToTop();
    }

    private void IconRows_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ColumnsFor(IconRows.ActualWidth) != _columns) RebuildRows();
    }

    private static int ColumnsFor(double width)
        => Math.Max(1, (int)((width - SystemParameters.VerticalScrollBarWidth - 2) / CellWidth));

    private void RebuildRows()
    {
        if (IconRows.ActualWidth <= 0) return;   // first pass runs from SizeChanged once laid out
        _columns = ColumnsFor(IconRows.ActualWidth);

        var rows = new List<IconCell[]>((_matches.Length + _columns - 1) / _columns);
        int currentRow = -1;
        for (int i = 0; i < _matches.Length; i += _columns)
        {
            var row = new IconCell[Math.Min(_columns, _matches.Length - i)];
            for (int c = 0; c < row.Length; c++)
            {
                var name = _matches[i + c];
                bool cur = name.Equals(SelectedIconName, StringComparison.OrdinalIgnoreCase);
                if (cur) currentRow = rows.Count;
                row[c] = new IconCell(name, cur);
            }
            rows.Add(row);
        }
        IconRows.ItemsSource = rows;

        // Opening scrolls the current glyph to the middle of the view; later rebuilds leave the scroll alone.
        if (_scrollToCurrent && currentRow >= 0)
        {
            _scrollToCurrent = false;
            Dispatcher.BeginInvoke(() =>
            {
                if (Scroller() is { } sv)
                    sv.ScrollToVerticalOffset(Math.Max(0, currentRow * RowHeight - (sv.ViewportHeight - RowHeight) / 2));
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private ScrollViewer? Scroller()
    {
        IconRows.ApplyTemplate();
        return IconRows.Template.FindName("Scroller", IconRows) as ScrollViewer;
    }

    private void IconButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedIconName = (sender as Button)?.Tag as string;
        DialogResult = true;
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        SelectedIconName = null;
        DialogResult = true;
    }
}
