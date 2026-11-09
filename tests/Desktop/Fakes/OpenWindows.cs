using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// Finds the windows a UX test's screen opened on the test's UI thread, through WPF's presentation sources, and
/// closes them when the test ends. Use it in a <c>using</c> declaration. Not a fake of a port: shared test setup.
/// </summary>
public sealed class OpenWindows : IDisposable
{
    /// <summary>The windows on this thread that are showing.</summary>
    public IReadOnlyList<Window> Visible() => [.. All().Where(w => w.IsVisible)];

    /// <summary>The logical descendants of the window, such as its text and buttons.</summary>
    public static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var grandChild in Descendants(child))
            {
                yield return grandChild;
            }
        }
    }

    /// <summary>
    /// The text a text block displays, including text made by its inlines (runs, bold). The one place tests read it,
    /// so every lookup agrees.
    /// </summary>
    public static string TextOf(TextBlock block) => new TextRange(block.ContentStart, block.ContentEnd).Text;

    /// <summary>The texts the window displays in text blocks and in the string content of labels and buttons.</summary>
    public static IReadOnlyList<string> TextsIn(Window window) =>
    [
        .. Descendants(window).Select(d => d switch
        {
            TextBlock block => TextOf(block),
            ContentControl { Content: string text } => text,
            _ => null,
        }).OfType<string>().Where(text => text.Length > 0),
    ];

    /// <summary>Clicks the button in the window whose text is <paramref name="text"/>, as the automation tools do.</summary>
    public static void Click(Window window, string text)
    {
        Click(Descendants(window).OfType<Button>().Single(b => b.Content as string == text));
    }

    /// <summary>Clicks the button, as the automation tools do. Returns after the click has been handled, since the tools queue it.</summary>
    public static void Click(Button button)
    {
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        button.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>Presses and releases Esc on the window, as the keyboard does: the tunneling event, then the bubbling one.</summary>
    public static void PressEscape(Window window)
    {
        SimulatedKey.Raise(window, Key.Escape, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);
    }

    /// <summary>Closes every window on this thread, even one that hides instead of closing.</summary>
    public void Dispose()
    {
        foreach (var source in Sources())
        {
            if (source.RootVisual is Window window)
            {
                try { window.Close(); } catch (InvalidOperationException) { }
            }

            source.Dispose();
        }
    }

    private static IEnumerable<Window> All() => Sources().Select(s => s.RootVisual).OfType<Window>();

    private static List<HwndSource> Sources() =>
        [.. PresentationSource.CurrentSources.OfType<HwndSource>().Where(s => !s.IsDisposed && s.CheckAccess())];
}
