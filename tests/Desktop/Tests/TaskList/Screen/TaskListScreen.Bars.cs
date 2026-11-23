using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// How a Task View test reaches the search box, the group headings' collapse, and the focus rules.
/// A group's tag badge has the automation id "Badge {tag}" (see
/// <see cref="TaskEditor.Tests.TagControls"/>); the "No tag" heading has no badge, so a click lands on the heading itself.
/// Shared test setup.
/// </summary>
internal sealed partial class TaskListScreen
{
    /// <summary>The search box.</summary>
    public TextBox SearchBox
    {
        get
        {
            Refresh();
            return Visuals(Window).OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Search");
        }
    }

    /// <summary>Clicks the group's tag badge, or the "No tag" heading, as the mouse does.</summary>
    public void ToggleGroup(string group)
    {
        MouseClickOn(GroupClickTarget(group));
        Refresh();
    }

    private FrameworkElement GroupClickTarget(string group)
    {
        Refresh();
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        return Visuals(heading).OfType<FrameworkElement>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e).StartsWith("Badge ", StringComparison.Ordinal))
            ?? heading;
    }

    private static void MouseClickOn(FrameworkElement target) => MouseClick(target, MouseButton.Left);
}
