using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Pick an installed application (the shell AppsFolder list — desktop + Store apps) for a
/// Launch App slice. Names show immediately; icons stream in on a background thread (each is a
/// stateless <see cref="IconCache.ExtractShellIcon"/> call returning a frozen bitmap). ShowDialog()
/// == true → <see cref="Selected"/> holds the pick.</summary>
public partial class AppPickerWindow : Window
{
    /// <summary>Row model: Icon raises change notification when the background loader fills it in.</summary>
    private sealed class Entry(InstalledApp app) : INotifyPropertyChanged
    {
        public InstalledApp App { get; } = app;
        public string Name => App.Name;

        private ImageSource? _icon;
        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly List<Entry> _all = [];
    private readonly ObservableCollection<Entry> _view = [];
    private bool _closed;

    public InstalledApp? Selected { get; private set; }

    public AppPickerWindow()
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        AppList.ItemsSource = _view;
        Closed += (_, _) => _closed = true;
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            // Scan on a dedicated STA thread (Shell.Application is an STA COM object) so a first-time
            // scan (~hundreds of items) never freezes the dialog.
            var apps = await Task.Run(() =>
            {
                InstalledApp[] result = [];
                var t = new Thread(() => result = InstalledApps.Scan());
                t.SetApartmentState(ApartmentState.STA);
                t.Start(); t.Join();
                return result;
            });
            if (_closed) return;
            foreach (var a in apps) _all.Add(new Entry(a));
            ApplyFilter();
            _ = Task.Run(LoadIconsAsync);
        };
    }

    /// <summary>Fill icons oldest-first off the UI thread. Assignment is marshalled back per item;
    /// stops quietly once the window closes.</summary>
    private void LoadIconsAsync()
    {
        foreach (var entry in _all)
        {
            if (_closed) return;
            var icon = IconCache.ExtractShellIcon(entry.App.LaunchPath, 64);
            if (icon is null) continue;
            Dispatcher.BeginInvoke(() => { if (!_closed) entry.Icon = icon; });
        }
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text.Trim();
        _view.Clear();
        foreach (var e in _all)
            if (q.Length == 0 || e.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase))
                _view.Add(e);
        if (_view.Count > 0 && AppList.SelectedIndex < 0) AppList.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

    private void AppList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        BtnOk.IsEnabled = AppList.SelectedItem is not null;

    private void AppList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (AppList.SelectedItem is Entry) Commit();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => Commit();

    private void Commit()
    {
        if (AppList.SelectedItem is not Entry entry) return;
        Selected = entry.App;
        DialogResult = true;
    }
}
