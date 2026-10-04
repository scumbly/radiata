using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControllerWheel;

/// <summary>A <see cref="Border"/> that keyboard and UI Automation can operate.
///
/// <para><b>A Border subclass, never a Button.</b> The Settings tile system and the icon well are plain
/// Borders whose plumbing finds them BY TYPE (`Children.OfType&lt;Border&gt;()`), mutates
/// <c>Child</c>/<c>Background</c>/<c>BorderBrush</c> directly, and (for the well) relies on
/// <c>MouseLeftButtonUp</c> bubbling from inner elements. Converting to Button breaks all of that silently
/// and needs a full ControlTemplate just to keep <c>CornerRadius</c>. This subclass still matches
/// <c>OfType&lt;Border&gt;()</c>, keeps every property and event as-is, and adds only: focusability,
/// Space/Enter firing <see cref="Invoked"/>, and an automation peer exposing <see cref="IInvokeProvider"/>
/// so Narrator/inspection tools see a real operable element.</para>
///
/// <para>The click handler stays where it is (XAML <c>MouseLeftButtonUp</c> or a factory lambda) —
/// <see cref="Invoked"/> must be the SAME action, stored so keyboard and UIA can reach it. Each call site
/// is responsible for keeping the two pointed at one method.</para></summary>
public class InvokableBorder : Border
{
    /// <summary>What pressing Space/Enter or a UIA Invoke performs — the same action as the mouse click.</summary>
    public Action? Invoked { get; set; }

    public InvokableBorder()
    {
        Focusable = true;
        KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Space or Key.Enter) || Invoked is null) return;
            e.Handled = true;
            Invoked();
        };
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new InvokablePeer(this);

    private sealed class InvokablePeer : FrameworkElementAutomationPeer, IInvokeProvider
    {
        private readonly InvokableBorder _owner;
        public InvokablePeer(InvokableBorder owner) : base(owner) => _owner = owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(InvokableBorder);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

        /// <summary>UIA calls arrive on a UIA worker thread; the action touches UI, so marshal.</summary>
        public void Invoke()
        {
            if (!IsEnabled()) throw new System.Windows.Automation.ElementNotEnabledException();
            _owner.Dispatcher.BeginInvoke(() => _owner.Invoked?.Invoke());
        }
    }
}
