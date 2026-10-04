using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace ControllerWheel;

/// <summary>UI Automation peer for <see cref="RadialMenuControl"/> (accessibility goal A1).
///
/// <para><b>What this is NOT for.</b> It is not how a blind user hears the wheel. The overlay is a
/// never-focused <c>WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</c> window, and Narrator does not service UIA
/// notifications or live regions from such a window. Speech comes from Radiata's own engine
/// (<see cref="Announcer"/> + <see cref="SpeechSink"/>).</para>
///
/// <para><b>What it IS for.</b> The wheel is drawing operations on a bare <c>FrameworkElement</c>, so
/// without this it has no automation tree at all: Accessibility Insights / inspect.exe see one anonymous
/// element, and no external tool can verify the wheel's semantics. This exposes the ring as a selection
/// container with one child per slice — name, position, and selection state — so:</para>
/// <list type="bullet">
///   <item>the wheel can be inspected with a UI Automation inspection tool (Accessibility Insights, Inspect.exe);</item>
///   <item>a future UI test can assert what the wheel BELIEVES it is showing, independently of pixels;</item>
///   <item>if Windows ever services notifications from non-activating windows, the tree is already here.</item>
/// </list>
///
/// <para>Names come from <see cref="WheelSlice.Label"/> — the same resolved string editing and launching
/// use — never from the visible-label setting, so artwork-only and logo slices are named too.</para>
///
/// <para><b>Invoke is deliberately not implemented.</b> A slice fires on TRIGGER RELEASE while armed, so
/// there is no "click this slice" operation to expose; a UIA client that invoked one would be doing
/// something the product cannot do. Selection is exposed read-only for the same reason.</para></summary>
public sealed class RadialMenuPeer : FrameworkElementAutomationPeer
{
    private readonly RadialMenuControl _wheel;
    // Rebuilt only when the slice COUNT changes: GetChildrenCore is called often, and the per-slice peers
    // must be stable objects or a client sees the whole tree churn on every refresh.
    private List<AutomationPeer>? _children;
    private int _childCount = -1;

    public RadialMenuPeer(RadialMenuControl owner) : base(owner) => _wheel = owner;

    protected override string GetClassNameCore() => nameof(RadialMenuControl);

    /// <summary>List, not Menu: the wheel has no invokable items (see class remarks).</summary>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override string GetNameCore() =>
        (_wheel.IsWheelB ? "Right wheel" : "Left wheel") + (_wheel.EditMode ? ", edit mode" : "");

    /// <summary>The wheel never takes focus — saying otherwise would invite a client to try.</summary>
    protected override bool IsKeyboardFocusableCore() => false;

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var slices = CurrentSlices;
        if (_children is null || _childCount != slices.Count)
        {
            _childCount = slices.Count;
            _children = new List<AutomationPeer>(slices.Count);
            for (int i = 0; i < slices.Count; i++) _children.Add(new SlicePeer(this, _wheel, i));
        }
        return _children;
    }

    /// <summary>What the wheel is currently DRAWING: the edit working-set or picker menu when one is up,
    /// else the configured slices. A peer that reported the configured list while a picker was on screen
    /// would be actively misleading.</summary>
    internal IReadOnlyList<WheelSlice> CurrentSlices =>
        _wheel.EditMode ? _wheel.EditCurrentSlices : _wheel.Slices;

    /// <summary>The slice list changed (a new wheel, edit mode starting/ending, a picker level, a structural
    /// edit): drop our per-slice peers AND WPF's own cached child list.
    /// <para>⚠ <see cref="AutomationPeer.GetChildren"/> caches internally — overriding
    /// <see cref="GetChildrenCore"/> alone is NOT enough — without this the tree keeps reporting the
    /// PREVIOUS wheel's slices forever.</para></summary>
    internal void InvalidateChildren()
    {
        _children = null;
        _childCount = -1;
        ResetChildrenCache();
    }

    /// <summary>Tell clients the selection moved. Safe to call when nothing is listening (the usual case) —
    /// <see cref="AutomationPeer.ListenerExists"/> keeps it to a no-op.</summary>
    internal void NotifySelectionChanged()
    {
        if (!ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected)) return;
        int armed = _wheel.ArmedIndex;
        var kids = GetChildrenCore();
        if ((uint)armed < (uint)kids.Count)
            kids[armed].RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }

    /// <summary>One slice. Read-only selection: the product has no "select this slice" operation that
    /// isn't "aim the stick at it", so <see cref="ISelectionItemProvider.Select"/> and friends refuse
    /// rather than pretending.</summary>
    private sealed class SlicePeer : AutomationPeer, ISelectionItemProvider
    {
        private readonly RadialMenuPeer _parent;
        private readonly RadialMenuControl _wheel;
        private readonly int _index;

        public SlicePeer(RadialMenuPeer parent, RadialMenuControl wheel, int index)
        { _parent = parent; _wheel = wheel; _index = index; }

        private WheelSlice? Slice
        {
            get
            {
                var s = _parent.CurrentSlices;
                return (uint)_index < (uint)s.Count ? s[_index] : null;
            }
        }

        protected override string GetClassNameCore() => "WheelSlice";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
        protected override string GetAutomationIdCore() => $"slice{_index}";

        /// <summary>The resolved label — independent of whether labels are DRAWN (logo/artwork slices
        /// suppress the visible text but always carry the name).</summary>
        protected override string GetNameCore() => Slice?.Label ?? "";

        /// <summary>Carries what the visuals carry: position, and the guarded-hold requirement — the one
        /// property that changes what firing this slice does.</summary>
        protected override string GetItemStatusCore()
        {
            var slices = _parent.CurrentSlices;
            string pos = $"{_index + 1} of {slices.Count}";
            bool guarded = Slice?.Action?.RequireConfirm == true;
            return guarded ? pos + ", hold to confirm" : pos;
        }

        protected override bool IsKeyboardFocusableCore() => false;
        protected override bool HasKeyboardFocusCore() => false;
        protected override bool IsEnabledCore() => true;
        protected override bool IsOffscreenCore() => !_wheel.IsVisible;
        protected override AutomationPeer GetLabeledByCore() => null!;
        protected override string GetAcceleratorKeyCore() => "";
        protected override string GetAccessKeyCore() => "";
        protected override string GetItemTypeCore() => "Wheel slice";
        protected override string GetHelpTextCore() => "";
        protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
        protected override List<AutomationPeer> GetChildrenCore() => new();
        protected override Rect GetBoundingRectangleCore() => _parent.GetBoundingRectangle();
        protected override Point GetClickablePointCore() => new(double.NaN, double.NaN);
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
        protected override bool IsPasswordCore() => false;
        protected override bool IsRequiredForFormCore() => false;
        protected override void SetFocusCore() { }   // cannot focus: the overlay never activates

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.SelectionItem ? this : null!;

        // ── ISelectionItemProvider (read-only) ────────────────────────────────────
        public bool IsSelected => _wheel.ArmedIndex == _index;
        public IRawElementProviderSimple? SelectionContainer =>
            ProviderFromPeer(_parent);
        public void AddToSelection() => throw new InvalidOperationException("The wheel's selection follows the stick.");
        public void RemoveFromSelection() => throw new InvalidOperationException("The wheel's selection follows the stick.");
        public void Select() => throw new InvalidOperationException("The wheel's selection follows the stick.");
    }
}
