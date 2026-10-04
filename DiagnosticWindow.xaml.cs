using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

public partial class DiagnosticWindow : Window
{
    private readonly ControllerReader _controller;
    private readonly ObservableCollection<ByteCell> _cells = [];
    private readonly ObservableCollection<InputPill> _pills = [];

    private byte[]? _baseline;
    private byte[]? _lastReport;
    private volatile bool _updatePending;
    private volatile bool _statePending;

    // Pill order — grouped as the hands find them, not as ControllerState declares them. Labels follow the
    // active glyph set, so an Xbox pad's readout names the buttons the tester is physically pressing.
    private static string[] PillNames() => ControllerButtons.Set == ControllerGlyphSet.Xbox
        ?
        [
            "A", "B", "X", "Y",
            "LB", "RB", "LT", "RT", "LS", "RS",
            "D-Up", "D-Down", "D-Left", "D-Right",
            "View", "Menu", "Guide",
        ]
        :
        [
            "Cross", "Circle", "Square", "Triangle",
            "L1", "R1", "L2", "R2", "L3", "R3",
            "D-Up", "D-Down", "D-Left", "D-Right",
            "Create", "Options", "PS",
        ];

    public DiagnosticWindow(ControllerReader controller)
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        _controller = controller;
        ByteGrid.ItemsSource = _cells;

        foreach (string n in PillNames()) _pills.Add(new InputPill(n));
        ButtonPills.ItemsSource = _pills;

        _controller.DiagnosticReport += OnReport;
        _controller.StateChanged    += OnState;
        ControllerButtons.Changed   += OnGlyphSetChanged;
        Closed += (_, _) =>
        {
            _controller.DiagnosticReport -= OnReport;
            _controller.StateChanged    -= OnState;
            ControllerButtons.Changed   -= OnGlyphSetChanged;
        };
    }

    // StateChanged is raised by BOTH backends, so this is what keeps the window useful on an Xbox pad,
    // where DiagnosticReport never fires at all.
    private void OnState(ControllerState s)
    {
        if (_statePending) return;
        _statePending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _statePending = false;
            ApplyState(s);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    // The glyph set can flip while the window is open (the Settings picker, or a different pad taking over).
    // Relabel in place — the pill ORDER never changes, only the names.
    private void OnGlyphSetChanged() => Dispatcher.InvokeAsync(() =>
    {
        string[] names = PillNames();
        for (int i = 0; i < _pills.Count && i < names.Length; i++) _pills[i].Rename(names[i]);
    });

    private void ApplyState(ControllerState s)
    {
        bool[] on =
        [
            s.Cross, s.Circle, s.Square, s.Triangle,
            // L2/R2 pills show the LATCHED digital state the chord matcher sees, not the raw byte —
            // the analog value is on the axis line below.
            s.L1, s.R1, _controller.L2Down, _controller.R2Down, s.L3, s.R3,
            s.DpadUp, s.DpadDown, s.DpadLeft, s.DpadRight,
            s.Create, s.Options, s.Ps,
        ];
        for (int i = 0; i < _pills.Count && i < on.Length; i++) _pills[i].SetOn(on[i]);

        int slot = _controller.XInputSlot;
        BackendText.Text = _controller.DetectionIssue ?? (slot >= 0
            ? $"Backend: XInput slot {slot} — {_controller.Kind}  (hex grid stays empty on this path)"
            : $"Backend: raw HID — {_controller.Kind} / {_controller.Transport}");

        // Only ApplyReport fills the header, and DiagnosticReport never fires on XInput — without this the
        // window reads "Waiting for controller…" forever on an Xbox pad that is in fact fully live.
        if (slot >= 0) HeaderText.Text = $"XInput slot {slot}  (no raw HID reports on this path)";

        AxisText.Text =
            $"LS {s.LeftStickX,6:+0.00;-0.00} {s.LeftStickY,6:+0.00;-0.00}   " +
            $"RS {s.RightStickX,6:+0.00;-0.00} {s.RightStickY,6:+0.00;-0.00}   " +
            $"L2 {s.LeftTrigger,3}   R2 {s.RightTrigger,3}   (digital: press ≥140, release ≤100)";
    }

    private void OnReport(byte[] report)
    {
        // Throttle UI updates to ~30 Hz
        if (_updatePending) return;
        _updatePending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _updatePending = false;
            ApplyReport(report);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ApplyReport(byte[] report)
    {
        if (report.Length == 0) return;
        string kind = report[0] switch
        {
            0x01 => "USB 0x01",
            0x31 => "BT  0x31",
            _    => $"0x{report[0]:X2}",
        };
        HeaderText.Text = $"{kind}  ({report.Length} bytes)";

        if (_baseline is null || _baseline.Length != report.Length) _baseline = new byte[report.Length];

        if (_cells.Count != report.Length)
        {
            _cells.Clear();
            for (int i = 0; i < report.Length; i++)
                _cells.Add(new ByteCell(i));
        }

        var changed = new List<string>();
        for (int i = 0; i < report.Length && i < _cells.Count; i++)
        {
            bool diff = report[i] != _baseline[i];
            _cells[i].Update(report[i], diff);
            if (diff)
                changed.Add($"[{i}] {_baseline[i]:X2}→{report[i]:X2}");
        }

        ChangedText.Text = changed.Count > 0
            ? "Changed:  " + string.Join("   ", changed)
            : ChangedText.Text; // keep last change visible

        _lastReport = report;
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReport is not null)
            _baseline = (byte[])_lastReport.Clone();

        foreach (var c in _cells) c.ClearDiff();
        ChangedText.Text = "Baseline reset.";
    }
}

/// <summary>View-model for one button pill in the semantic readout.</summary>
public sealed class InputPill : INotifyPropertyChanged
{
    private static readonly Brush OffBg = new SolidColorBrush(Color.FromRgb(40, 40, 40));
    private static readonly Brush OnBg  = new SolidColorBrush(Color.FromRgb(60, 170, 90));
    private static readonly Brush OffFg = Brushes.Gray;
    private static readonly Brush OnFg  = Brushes.Black;

    static InputPill()
    {
        OffBg.Freeze(); OnBg.Freeze(); OffFg.Freeze(); OnFg.Freeze();
    }

    public string Name { get => _name; private set { _name = value; OnChanged(); } }
    public Brush Background { get => _bg; private set { _bg = value; OnChanged(); } }
    public Brush Foreground { get => _fg; private set { _fg = value; OnChanged(); } }

    private Brush  _bg = OffBg;
    private Brush  _fg = OffFg;
    private bool   _on;
    private string _name;

    public InputPill(string name) { _name = name; }

    public void Rename(string name) => Name = name;

    public void SetOn(bool on)
    {
        if (on == _on) return;          // brush churn per report otherwise
        _on = on;
        Background = on ? OnBg : OffBg;
        Foreground = on ? OnFg : OffFg;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>View-model for one byte cell in the grid.</summary>
public sealed class ByteCell : INotifyPropertyChanged
{
    private static readonly Brush NormalBg  = new SolidColorBrush(Color.FromRgb(40, 40, 40));
    private static readonly Brush ChangedBg = new SolidColorBrush(Color.FromRgb(200, 160, 0));
    private static readonly Brush NormalFg  = Brushes.LightGray;
    private static readonly Brush ChangedFg = Brushes.Black;

    static ByteCell()
    {
        NormalBg.Freeze(); ChangedBg.Freeze();
        NormalFg.Freeze(); ChangedFg.Freeze();
    }

    public int    IndexVal  { get; }
    public string Index     => IndexVal.ToString();
    public string Hex       { get => _hex;        private set { _hex = value;        OnChanged(); } }
    public Brush  Background{ get => _bg;         private set { _bg = value;         OnChanged(); } }
    public Brush  Foreground{ get => _fg;         private set { _fg = value;         OnChanged(); } }
    public string Tip       { get => _tip;        private set { _tip = value;        OnChanged(); } }

    private string _hex = "??";
    private Brush  _bg  = NormalBg;
    private Brush  _fg  = NormalFg;
    private string _tip = "";

    public ByteCell(int index) { IndexVal = index; }

    public void Update(byte value, bool changed)
    {
        Hex        = $"{value:X2}";
        Background = changed ? ChangedBg : NormalBg;
        Foreground = changed ? ChangedFg : NormalFg;
        Tip        = $"byte {IndexVal} = 0x{value:X2} ({value})";
    }

    public void ClearDiff()
    {
        Background = NormalBg;
        Foreground = NormalFg;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
