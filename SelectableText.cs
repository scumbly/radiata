using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;   // disambiguate from WinForms (referenced app-wide)
using Cursors    = System.Windows.Input.Cursors;
using ScrollBar  = System.Windows.Controls.Primitives.ScrollBar;

namespace ControllerWheel;

/// <summary>Makes plain <see cref="TextBlock"/>s mouse-selectable (drag-select + Ctrl+C) by attaching WPF's
/// internal <c>System.Windows.Documents.TextEditor</c> — the engine TextBox uses; WPF never exposed it on
/// TextBlock. Reflection into WPF internals, so every step is FAIL-SOFT: the first reflection failure turns
/// the feature off for the session and the text simply stays unselectable (nothing throws). Interactive text
/// keeps its normal click feel — the walker skips buttons/combos/inputs and anything under a Hand-cursor
/// click target (material/thickness tiles). Used by onboarding so testers can copy driver logs, status lines,
/// and instructions, and by Help so every topic can be copied.
/// <para>Hyperlink-bearing blocks ARE selectable: the editor is attached read-only, which leaves
/// <c>Hyperlink.IsEditable</c> false, so links still navigate on a plain click instead of demanding
/// Ctrl+click. Don't reintroduce a skip for them — it makes whole paragraphs uncopyable.</para></summary>
internal static class SelectableText
{
    // Holds the attached TextEditor: keeps it alive for the TextBlock's lifetime + doubles as the done-flag.
    private static readonly DependencyProperty EditorProperty = DependencyProperty.RegisterAttached(
        "Editor", typeof(object), typeof(SelectableText), new PropertyMetadata(null));

    private static readonly Type? EditorType =
        typeof(FrameworkElement).Assembly.GetType("System.Windows.Documents.TextEditor");
    private static readonly Type? ContainerType =
        typeof(FrameworkElement).Assembly.GetType("System.Windows.Documents.ITextContainer");
    private static readonly PropertyInfo? TextContainerProp =
        typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly PropertyInfo? IsReadOnlyProp =
        EditorType?.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly PropertyInfo? TextViewProp =
        EditorType?.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly PropertyInfo? ContainerTextViewProp = ContainerType?.GetProperty("TextView");

    private static bool _broken;              // reflection failed once → feature off for this session
    private static bool _handlersRegistered;  // TextEditor's class handlers for TextBlock (once per app)

    /// <summary>Enable selection on every eligible TextBlock under <paramref name="root"/>. Idempotent —
    /// call again whenever elements appear (step changes, code-built rows).</summary>
    public static void EnableWithin(DependencyObject root)
    {
        if (_broken) return;
        try { Walk(root); }
        catch { _broken = true; }
    }

    private static void Walk(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            // Don't descend into interactive controls — selecting their label text would fight clicks.
            if (child is ButtonBase or ComboBox or ComboBoxItem or ScrollBar or PasswordBox
                     or System.Windows.Controls.TextBox) continue;
            if (child is FrameworkElement fe && fe.Cursor == Cursors.Hand) continue;   // tiles/cards
            if (child is TextBlock tb) Enable(tb);
            else Walk(child);
        }
    }

    private static void Enable(TextBlock tb)
    {
        if (tb.GetValue(EditorProperty) is not null) return;
        if (EditorType is null || TextContainerProp is null || IsReadOnlyProp is null ||
            TextViewProp is null || ContainerTextViewProp is null) { _broken = true; return; }

        RegisterHandlersOnce();
        var container = TextContainerProp.GetValue(tb);
        if (container is null) return;
        // internal TextEditor(ITextContainer, FrameworkElement uiScope, bool isUndoEnabled)
        var editor = Activator.CreateInstance(EditorType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
            null, [container, tb, false], null);
        if (editor is null) return;
        IsReadOnlyProp.SetValue(editor, true);
        TextViewProp.SetValue(editor, ContainerTextViewProp.GetValue(container));
        tb.Focusable = true;                          // Ctrl+C needs keyboard focus (a click provides it)…
        KeyboardNavigation.SetIsTabStop(tb, false);   // …but stay out of the wizard's Tab order
        tb.Cursor = Cursors.IBeam;
        tb.SetValue(EditorProperty, editor);
    }

    private static void RegisterHandlersOnce()
    {
        if (_handlersRegistered) return;
        _handlersRegistered = true;
        // Class-level selection/copy command + input bindings for TextBlock — no-ops on TextBlocks
        // without an attached editor (i.e. everywhere outside onboarding).
        EditorType!.GetMethod("RegisterCommandHandlers", BindingFlags.Static | BindingFlags.NonPublic,
                null, [typeof(Type), typeof(bool), typeof(bool), typeof(bool)], null)
            ?.Invoke(null, [typeof(TextBlock), true, true, true]);
    }
}
