using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControllerWheel;

/// <summary>Space/Enter opens a closed <see cref="ComboBox"/> from the keyboard. WPF's own ComboBox only
/// wires F4 and Alt+Up/Down for this: tabbing to a Settings drop-down and pressing either of the two keys
/// most people try first does nothing.
/// <para>An attached property rather than an EventSetter with a named handler: <c>SettingsTheme.xaml</c> is a
/// plain <c>ResourceDictionary</c> with no code-behind class for an EventSetter to resolve against, and this
/// keeps the fix in the Style, applying to every Settings combo without touching each XAML call site.</para>
/// <para>Once open, arrow keys/Enter/Escape already work — that's built into <c>ComboBox</c> itself,
/// independent of the template — so this only needs to cover getting it OPEN in the first place.</para></summary>
public static class ComboBoxKeyOpen
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ComboBoxKeyOpen), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox box) return;
        box.PreviewKeyDown -= OnPreviewKeyDown;   // idempotent against a Style re-application
        if ((bool)e.NewValue) box.PreviewKeyDown += OnPreviewKeyDown;
    }

    private static void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var box = (ComboBox)sender;
        if (box.IsDropDownOpen || e.Key is not (Key.Space or Key.Enter)) return;
        box.IsDropDownOpen = true;
        e.Handled = true;
    }
}
