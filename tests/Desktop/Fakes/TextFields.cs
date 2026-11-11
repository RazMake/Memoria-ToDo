using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// How tests find and drive a text field by its automation id, whichever text control it is: a text box or the styled text
/// editor. The one place that knows both, so screens that host the Task editor read its fields the same way.
/// </summary>
public static class TextFields
{
    /// <summary>The element below <paramref name="root"/> with the automation id.</summary>
    public static FrameworkElement Find(DependencyObject root, string id) =>
        Below(root).Single(element => AutomationProperties.GetAutomationId(element) == id);

    /// <summary>The element below <paramref name="root"/> with the automation id that is showing, or null when there is none.</summary>
    public static FrameworkElement? FindShowing(DependencyObject root, string id) =>
        Below(root).SingleOrDefault(element => element.IsVisible && AutomationProperties.GetAutomationId(element) == id);

    // What is below the root in the logical tree or in the visual tree, so a field inside a template is found too.
    private static IEnumerable<FrameworkElement> Below(DependencyObject root) =>
        OpenWindows.Descendants(root).Concat(VisualDescendants(root)).OfType<FrameworkElement>().Distinct();

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        if (parent is not (Visual or System.Windows.Media.Media3D.Visual3D))
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var inner in VisualDescendants(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>Sets what the user has typed in the field, as typing does.</summary>
    public static void Type(FrameworkElement field, string text)
    {
        switch (field)
        {
            case TextEditor editor:
                editor.Text = text;
                break;
            case TextBox box:
                box.Text = text;
                break;
            default:
                throw new InvalidOperationException($"{field.GetType().Name} isn't a text field.");
        }
    }

    /// <summary>What the field's text holds now.</summary>
    public static string TextOf(FrameworkElement field) =>
        field switch
        {
            TextEditor editor => editor.Text,
            TextBox box => box.Text,
            _ => throw new InvalidOperationException($"{field.GetType().Name} isn't a text field."),
        };
}
