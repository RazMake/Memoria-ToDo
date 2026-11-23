using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// How a Task View test reaches the tab headers of Edit Description, the preview's page, and the keyboard order.
/// A tab header is a button named "Edit" or "Preview" (the automation name, or its text); the selected one has automation
/// item status "Selected", as a selected row has. The preview shows in the one web view, so the page it shows is the
/// fake web view's last page, which carries the browser's resize rule exactly when it is a preview page.
/// Shared test setup.
/// </summary>
internal sealed partial class TaskListScreen
{
    private IEnumerable<ButtonBase> TabButtons() =>
        Buttons(Window).Where(button => LabelOf(button) is "Edit" or "Preview");

    /// <summary>The labels of the tab headers showing, left to right; empty while no description is edited.</summary>
    public IReadOnlyList<string> TabHeaders =>
        [.. TabButtons().OrderBy(button => button.TranslatePoint(new Point(0, 0), Window).X).Select(LabelOf)];

    /// <summary>Clicks the tab header with the label, as the mouse does.</summary>
    public void ClickTab(string label) =>
        Activate(TabButtons().Single(button => LabelOf(button) == label));

    /// <summary>Whether a Title editor shows in the row, such as the second of a task's two rows.</summary>
    public bool IsEditingTitle(string title, string? group) => TextFields.FindShowing(Row(title, group), "Title") is not null;
}
