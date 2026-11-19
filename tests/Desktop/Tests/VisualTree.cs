using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop;

/// <summary>How screen tests walk a window's visual tree and click its buttons. Shared test setup.</summary>
internal static class VisualTree
{
    /// <summary>Everything below the element in the visual tree, in the order it shows.</summary>
    public static IEnumerable<DependencyObject> Visuals(DependencyObject parent)
    {
        if (parent is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (var inner in Visuals(child))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>The buttons showing below the root, check boxes left out.</summary>
    public static IEnumerable<ButtonBase> VisibleButtons(DependencyObject root) =>
        Visuals(root).OfType<ButtonBase>().Where(button => button.IsVisible && button is not CheckBox);

    /// <summary>The button's accessible name, else its text.</summary>
    public static string LabelOf(ButtonBase button) =>
        AutomationProperties.GetName(button) is { Length: > 0 } name ? name : button.Content as string ?? "";

    /// <summary>Presses and releases the mouse button on the element, as the mouse does: the tunneling event, then the bubbling one, for the press and the release.</summary>
    public static void MouseClick(UIElement target, MouseButton button)
    {
        foreach (var (tunneling, bubbling) in new[]
        {
            (Mouse.PreviewMouseDownEvent, Mouse.MouseDownEvent),
            (Mouse.PreviewMouseUpEvent, Mouse.MouseUpEvent),
        })
        {
            foreach (var routed in new[] { tunneling, bubbling })
            {
                target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
                {
                    RoutedEvent = routed,
                    Source = target,
                });
            }
        }
    }

    /// <summary>Clicks the way the automation tools do, then lets the click's work finish.</summary>
    public static void Activate(ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
        {
            invoke.Invoke();
        }
        else
        {
            ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle();
        }

        button.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }
}
