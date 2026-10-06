using System.Windows;

namespace ControllerWheel;

public partial class HidWizardWindow : Window
{
    private enum Step { WaitConnect, Baseline, FnLeft, FnRight, DPad, Done }

    private Step _step = Step.WaitConnect;

    // ── Baseline data ─────────────────────────────────────────────────────────
    private byte[]?      _baseline;
    private HashSet<int> _noisyBytes  = [];
    private int          _baselineFrameCount;
    private byte[]?      _baselineMin, _baselineMax;
    private const int    BaselineFrames = 375; // ~1.5 s at 250 Hz

    // ── Detection ─────────────────────────────────────────────────────────────
    private record struct Detection(int ByteIndex, int Value);
    private Detection? _candidate;
    private int        _candidateFrames;
    private const int  ConfirmFrames = 8; // ~32 ms

    // After confirmation, wait here until the button is physically released
    private bool      _waitingForRelease;
    private Detection _confirmedResult;
    private Step      _nextStep;

    // ── Results ───────────────────────────────────────────────────────────────
    private Detection? _fnLeft, _fnRight, _dpad;

    // Bytes never used for Fn detection:
    //   0-7  : report ID, stick axes, trigger axes, sequence counter
    //   12-33: DualSense sensor area (gyro 16-21, accel 22-27, timestamp 28-29, misc 12-15 / 30-33)
    private static readonly HashSet<int> AlwaysExclude =
        new(Enumerable.Range(0, 8).Concat(Enumerable.Range(12, 22)));

    private readonly ControllerReader _controller;

    public HidWizardWindow(ControllerReader controller)
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        _controller = controller;
        _controller.CalibrationReport += OnReport;
        Closed += (_, _) => _controller.CalibrationReport -= OnReport;
        EnterStep(Step.WaitConnect);
    }

    // ── Report dispatch (background → UI thread) ──────────────────────────────

    private void OnReport(byte[] report) =>
        Dispatcher.InvokeAsync(() => ProcessReport(report));

    private void ProcessReport(byte[] report)
    {
        if (_waitingForRelease) { HandleRelease(report); return; }

        switch (_step)
        {
            case Step.WaitConnect:  EnterStep(Step.Baseline); break;
            case Step.Baseline:     CollectBaseline(report);   break;
            case Step.FnLeft:
            case Step.FnRight:      DetectBit(report);         break;
            case Step.DPad:         DetectDPad(report);        break;
        }
    }

    private void EnterStep(Step step)
    {
        _step            = step;
        _candidate       = null;
        _candidateFrames = 0;
        _waitingForRelease = false;

        InstructionPanel.Visibility = Visibility.Visible;
        ResultsPanel.Visibility     = Visibility.Collapsed;
        BtnSkip.Visibility          = Visibility.Collapsed;
        BtnSave.Visibility          = Visibility.Collapsed;
        BtnRestart.Visibility       = Visibility.Collapsed;

        switch (step)
        {
            case Step.WaitConnect:
                StepText.Text        = "";
                InstructionText.Text = Loc.T(UiText.Wizards.HidWaiting);
                SubText.Text         = Loc.T(UiText.Wizards.HidConnect);
                StatusText.Text      = "";
                break;

            case Step.Baseline:
                StepText.Text        = Loc.T(UiText.Wizards.HidCalibrating);
                InstructionText.Text = Loc.T(UiText.Wizards.HidHoldNaturally);
                SubText.Text         = Loc.T(UiText.Wizards.HidNoButtons);
                StatusText.Text      = Loc.T(UiText.Wizards.HidCollecting);
                _baselineFrameCount  = 0;
                _baselineMin         = null;
                _baselineMax         = null;
                break;

            case Step.FnLeft:
                StepText.Text        = Loc.T(UiText.Wizards.HidStep1);
                InstructionText.Text = Loc.T(UiText.Wizards.HidPressLeftFn);
                SubText.Text         = Loc.T(UiText.Wizards.HidLeftFnWhere);
                StatusText.Text      = Loc.T(UiText.Wizards.HidListening);
                BtnSkip.Visibility   = Visibility.Visible;
                BtnRestart.Visibility = Visibility.Visible;
                break;

            case Step.FnRight:
                StepText.Text        = Loc.T(UiText.Wizards.HidStep2);
                InstructionText.Text = Loc.T(UiText.Wizards.HidPressRightFn);
                SubText.Text         = Loc.T(UiText.Wizards.HidRightFnWhere);
                StatusText.Text      = Loc.T(UiText.Wizards.HidListening);
                BtnSkip.Visibility   = Visibility.Visible;
                BtnRestart.Visibility = Visibility.Visible;
                break;

            case Step.DPad:
                StepText.Text        = Loc.T(UiText.Wizards.HidStep3);
                InstructionText.Text = Loc.T(UiText.Wizards.HidPressDpadUp);
                SubText.Text         = Loc.T(UiText.Wizards.HidDpadUpWhere);
                StatusText.Text      = Loc.T(UiText.Wizards.HidListening);
                BtnSkip.Visibility   = Visibility.Visible;
                BtnRestart.Visibility = Visibility.Visible;
                break;

            case Step.Done:
                StepText.Text              = Loc.T(UiText.Wizards.HidComplete);
                InstructionPanel.Visibility = Visibility.Collapsed;
                ResultsPanel.Visibility     = Visibility.Visible;
                BtnSave.Visibility          = Visibility.Visible;
                ShowResults();
                break;
        }
    }

    private static Step Next(Step s) => s switch
    {
        Step.FnLeft  => Step.FnRight,
        Step.FnRight => Step.DPad,
        Step.DPad    => Step.Done,
        _            => Step.Done,
    };

    private void CollectBaseline(byte[] report)
    {
        if (_baselineMin is null || _baselineMin.Length != report.Length)
        {
            _baselineMin = (byte[])report.Clone();
            _baselineMax = (byte[])report.Clone();
        }

        for (int i = 0; i < report.Length; i++)
        {
            if (report[i] < _baselineMin[i]) _baselineMin[i] = report[i];
            if (report[i] > _baselineMax![i]) _baselineMax[i] = report[i];
        }

        _baselineFrameCount++;
        StatusText.Text = Loc.F(UiText.Wizards.HidCollectingPct, _baselineFrameCount * 100 / BaselineFrames);

        if (_baselineFrameCount < BaselineFrames) return;

        _baseline   = (byte[])report.Clone();
        _noisyBytes = [];
        for (int i = 0; i < report.Length; i++)
            if (_baselineMax![i] != _baselineMin[i]) _noisyBytes.Add(i);

        EnterStep(Step.FnLeft);
    }

    // ── Bit-change detection (Fn buttons) ────────────────────────────────────

    private void DetectBit(byte[] report)
    {
        if (_baseline is null || report.Length != _baseline.Length) return;

        Detection? found = null;
        for (int i = 0; i < report.Length; i++)
        {
            if (AlwaysExclude.Contains(i) || _noisyBytes.Contains(i)) continue;
            if (report[i] == _baseline[i]) continue;

            byte diff = (byte)(report[i] ^ _baseline[i]);
            if (IsPow2(diff) && (report[i] & diff) != 0) // bit must be SET, not cleared
            {
                found = new Detection(i, diff);
                break;
            }
        }

        Confirm(found, report,
            onConfirmed: r =>
            {
                StatusText.Text = Loc.F(UiText.Wizards.HidDetectedMask, _confirmedResult.ByteIndex, _confirmedResult.Value.ToString("X2"));
            });
    }

    // ── D-pad lower-nibble detection ──────────────────────────────────────────

    private void DetectDPad(byte[] report)
    {
        if (_baseline is null || report.Length != _baseline.Length) return;

        Detection? found = null;
        for (int i = 0; i < report.Length; i++)
        {
            if (_noisyBytes.Contains(i)) continue; // D-pad at byte 8, don't use AlwaysExclude

            byte lo     = (byte)(report[i]    & 0x0F);
            byte baseLo = (byte)(_baseline[i] & 0x0F);
            if (lo != baseLo && lo <= 7)
            {
                found = new Detection(i, lo);
                break;
            }
        }

        Confirm(found, report,
            onConfirmed: _ =>
            {
                StatusText.Text = Loc.F(UiText.Wizards.HidDetected, _confirmedResult.ByteIndex);
            });
    }

    private void Confirm(Detection? found, byte[] report, Action<byte[]> onConfirmed)
    {
        if (found is not null && found == _candidate)
        {
            _candidateFrames++;
            StatusText.Text = Loc.F(UiText.Wizards.HidCandidate, found.Value.ByteIndex, _candidateFrames, ConfirmFrames);
        }
        else
        {
            _candidate       = found;
            _candidateFrames = found is not null ? 1 : 0;
            if (found is null) StatusText.Text = Loc.T(UiText.Wizards.HidListening);
        }

        if (_candidateFrames < ConfirmFrames) return;

        _confirmedResult   = _candidate!.Value;
        _nextStep          = Next(_step);
        _waitingForRelease = true;
        onConfirmed(report);
    }

    private void HandleRelease(byte[] report)
    {
        if (_baseline is null || report.Length <= _confirmedResult.ByteIndex) return;

        bool released;
        if (_step == Step.DPad)
            released = (report[_confirmedResult.ByteIndex] & 0x0F) == 8; // neutral nibble
        else
            released = (report[_confirmedResult.ByteIndex] & _confirmedResult.Value) == 0; // bit cleared

        if (!released) return;

        if (_step == Step.FnLeft)  _fnLeft  = _confirmedResult;
        if (_step == Step.FnRight) _fnRight = _confirmedResult;
        if (_step == Step.DPad)    _dpad    = _confirmedResult;

        _baseline = (byte[])report.Clone(); // fresh baseline for next step
        EnterStep(_nextStep);
    }

    private void ShowResults()
    {
        SetResult(ResFnLeft,  IcoFnLeft,  _fnLeft,  isBit: true);
        SetResult(ResFnRight, IcoFnRight, _fnRight, isBit: true);
        SetResult(ResDPad,    IcoDPad,    _dpad,    isBit: false);
        StatusText.Text = Loc.T(UiText.Wizards.HidReview);
    }

    private static void SetResult(
        System.Windows.Controls.TextBlock valBlock,
        System.Windows.Controls.TextBlock icoBlock,
        Detection? d, bool isBit)
    {
        if (d is null)
        {
            valBlock.Text       = Loc.T(UiText.Wizards.HidSkipped);
            valBlock.Foreground = System.Windows.Media.Brushes.Gray;
            icoBlock.Text       = "⚠";
            icoBlock.Foreground = System.Windows.Media.Brushes.Orange;
        }
        else
        {
            valBlock.Text = isBit
                ? Loc.F(UiText.Wizards.HidByteMask, d.Value.ByteIndex, d.Value.Value.ToString("X2"))
                : Loc.F(UiText.Wizards.HidByte, d.Value.ByteIndex);
            valBlock.Foreground = System.Windows.Media.Brushes.Black;
            icoBlock.Text       = "✓";
            icoBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x89, 0xB2, 0x9A));   // app green
        }
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e) =>
        EnterStep(Next(_step));

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        _fnLeft = _fnRight = _dpad = null;
        EnterStep(Step.Baseline);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new HidOffsets();
        var offsets = new HidOffsets
        {
            FnLeftByte  = _fnLeft?.ByteIndex  ?? defaults.FnLeftByte,
            FnLeftMask  = _fnLeft?.Value      ?? defaults.FnLeftMask,
            FnRightByte = _fnRight?.ByteIndex ?? defaults.FnRightByte,
            FnRightMask = _fnRight?.Value     ?? defaults.FnRightMask,
            DPadByte    = _dpad?.ByteIndex    ?? defaults.DPadByte,
        };
        try { offsets.Save(); }
        catch (Exception ex)
        {
            StatusText.Text = Loc.F(UiText.Settings.SaveFailed, ex.Message);
            return;
        }
        _controller.ApplyOffsets(offsets);
        StatusText.Text   = Loc.T(UiText.Wizards.HidSaved);
        BtnSave.IsEnabled = false;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private static bool IsPow2(byte b) => b != 0 && (b & (b - 1)) == 0;
}
