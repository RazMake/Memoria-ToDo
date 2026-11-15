using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.PlatformTestHost;

/// <summary>
/// The pointer scenario: which controls show the hand pointer once Run the app has set up the process. It prints one line per kind:
/// <c>cursor &lt;kind&gt; hand=True|False</c> for <c>Button</c>, <c>ToggleButton</c>, <c>CheckBox</c> and <c>RadioButton</c>, and
/// <c>cursor ScrollBarArrows count=&lt;n&gt; hand=&lt;n&gt;</c>, the repeat buttons found in a scroll bar and how many of them show the hand.
/// </summary>
internal static class CursorScenarios
{
    // Start asks each kind of button which pointer it shows, as WPF does when the mouse is over it, then ends the app.
    public static void ButtonKinds(Action endApp)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (var (kind, control) in new (string, Control)[]
            {
                ("Button", new Button()),
                ("ToggleButton", new ToggleButton()),
                ("CheckBox", new CheckBox()),
                ("RadioButton", new RadioButton()),
            })
            {
                ScenarioLifetime.Say($"cursor {kind} hand={ShowsHand(control)}");
            }

            var scrollBar = new ScrollBar { Maximum = 100, ViewportSize = 10, Height = 200 };
            var window = new Window { Content = scrollBar, Width = 100, Height = 300 };
            window.Show();
            scrollBar.UpdateLayout();
            var arrows = Descendants(scrollBar).OfType<RepeatButton>().ToList();
            ScenarioLifetime.Say($"cursor ScrollBarArrows count={arrows.Count} hand={arrows.Count(ShowsHand)}");
            window.Close();
            endApp();
        });
    }

    // Raises the query WPF raises on the element under the mouse, and reads the pointer that a handler chose.
    private static bool ShowsHand(Control control)
    {
        var query = new QueryCursorEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.QueryCursorEvent };
        control.RaiseEvent(query);
        return ReferenceEquals(query.Cursor, Cursors.Hand);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var grandChild in Descendants(child))
            {
                yield return grandChild;
            }
        }
    }
}
