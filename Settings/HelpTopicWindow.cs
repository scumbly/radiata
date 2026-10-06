using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>A small pop-up showing ONE <see cref="HelpContent"/> topic, for surfaces that have a (?) link
/// but no Help tab to send the user to — the onboarding wizard's Accessibility tip card is the first.
///
/// <para>It renders through <see cref="HelpEditorControl.RenderTopicInto"/>, the same path the Help tab
/// uses, so the pop-up cannot drift from Help: adding an accessibility feature means adding a bullet to
/// <c>HelpContent</c>'s "accessibility" topic, and both surfaces pick it up. Never copy the text into the
/// wizard instead.</para>
///
/// <para>Cross-links render as plain bold here: a one-topic window has nowhere to navigate to.</para></summary>
internal static class HelpTopicWindow
{
    /// <summary>Show <paramref name="topicId"/> modally over <paramref name="owner"/>. Unknown id = no-op,
    /// so a stale topic id degrades to "the link does nothing" rather than taking the wizard down.</summary>
    public static void Show(Window owner, string topicId,
                            Func<string, string?>? resolve = null, string? languageCode = null)
    {
        string lang = HelpLocalization.Normalize(languageCode);
        var topic = HelpLocalization.Topics(lang)
            .FirstOrDefault(t => t.Id.Equals(topicId, StringComparison.OrdinalIgnoreCase));
        if (topic is null)
        {
            System.Diagnostics.Trace.WriteLine($"[Help] pop-up asked for unknown topic '{topicId}'");
            return;
        }

        var body = new StackPanel();
        HelpEditorControl.RenderTopicInto(body, topic,
            resolve ?? HelpContent.ExportToken, lang, navigate: null);

        var close = new Button
        {
            Content = Loc.T(UiText.Common.Close), Padding = new Thickness(16, 6, 16, 6),
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0),
            IsDefault = true, IsCancel = true,
        };
        var root = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };
        DockPanel.SetDock(close, Dock.Bottom);
        root.Children.Add(close);
        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body,
        });

        var dlg = new Window
        {
            FlowDirection = LocWpf.Flow,
            Title = HelpContent.ResolveTokens(topic.Title, resolve ?? HelpContent.ExportToken),
            Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 520, MaxHeight = 620, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            Background = Brushes.White, Content = root,
        };
        close.Click += (_, _) => dlg.Close();
        dlg.ShowDialog();
    }
}
