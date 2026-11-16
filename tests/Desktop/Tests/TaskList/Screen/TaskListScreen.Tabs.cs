using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskEditor.Tests;

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

    /// <summary>The label of the selected tab header, or null while none shows.</summary>
    public string? SelectedTab =>
        TabButtons().Where(button => AutomationProperties.GetItemStatus(button) == "Selected").Select(LabelOf).SingleOrDefault();

    /// <summary>Clicks the tab header with the label, as the mouse does.</summary>
    public void ClickTab(string label) =>
        Activate(TabButtons().Single(button => LabelOf(button) == label));

    /// <summary>Whether any tab header is a keyboard stop, so Tab reaches it.</summary>
    public bool TabHeadersAreTabStops => TabButtons().Any(button => button.Focusable && KeyboardNavigation.GetIsTabStop(button));

    /// <summary>Whether any tab header draws a focus rectangle of its own.</summary>
    public bool TabHeadersDrawFocusRectangles => TabButtons().Any(button => button.FocusVisualStyle is not null);

    /// <summary>Whether the page the web view shows now is a preview page, which has resize corners.</summary>
    public bool PageIsPreview => Web.Page?.Contains("resize:horizontal", StringComparison.Ordinal) == true;

    /// <summary>Whether the keyboard focus is in the right side but not in the editor, so its arrow keys scroll it.</summary>
    public bool FocusIsInDetailsOutsideTheEditor
    {
        get
        {
            Refresh();
            var focused = FocusedElement as DependencyObject;
            return focused is not null && (focused == Details || Details.IsAncestorOf(focused)) && !FocusIsInEditor;
        }
    }

    /// <summary>
    /// Clicks the selected task's formatted description with the left mouse button, as a user does. The description's page never
    /// holds the focus, so the right side's pane takes it, as a ScrollViewer does on a left click; this fails when it doesn't.
    /// </summary>
    public void ClickDescription()
    {
        Refresh();
        MouseClick(Visuals(Details).OfType<FormattedView.FormattedDescription>().Single(), MouseButton.Left);
        Refresh();
        if (!FocusIsInDetailsOutsideTheEditor)
        {
            throw new InvalidOperationException("Clicking the description didn't move the keyboard focus to the right side.");
        }
    }

    /// <summary>Puts the cursor of the edited Description at the position, with nothing selected.</summary>
    public void PutCursorInDescription(int position) => EditorFields.Select(Window, "Description", position);

    /// <summary>The start and length of the edited Description's selection.</summary>
    public (int Start, int Length) DescriptionSelection => EditorFields.SelectionOf(Window, "Description");

    /// <summary>Whether the keyboard focus is the row or inside it, such as in its Title editor.</summary>
    public bool FocusIsInRow(string title, string? group = null) =>
        FocusedElement is DependencyObject focused && (focused == Row(title, group) || Row(title, group).IsAncestorOf(focused));

    /// <summary>Whether a Title editor shows in the row, such as the second of a task's two rows.</summary>
    public bool IsEditingTitle(string title, string? group) => TextFields.FindShowing(Row(title, group), "Title") is not null;

    /// <summary>
    /// The names of the keyboard stops in the window in Tab order, from the first: a row by its title, the search box
    /// "Search", a button by its label or tooltip.
    /// </summary>
    public IReadOnlyList<string> TabOrder => [.. KeyboardStops().Select(NameOfStop)];

    /// <summary>Whether the element is itself a keyboard stop: Tab traversal lands on it, not only on elements inside it.</summary>
    public bool IsKeyboardStop(UIElement element) => KeyboardStops().Contains(element);

    // The elements Tab lands on in the window, from the first, found by moving the focus as Tab does until it comes round.
    private List<UIElement> KeyboardStops()
    {
        Refresh();
        var stops = new List<UIElement>();
        Keyboard.ClearFocus();
        Window.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        while (FocusedElement is UIElement stop && stops.Count < 100 && !stops.Contains(stop))
        {
            stops.Add(stop);
            stop.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        return stops;
    }

    /// <summary>Whether the description element, the page of the one web view, is shown rather than hidden.</summary>
    public bool DescriptionIsShown
    {
        get
        {
            Refresh();
            return Visuals(Details).OfType<FormattedView.FormattedDescription>().Single().IsVisible;
        }
    }

    private static string NameOfStop(UIElement stop) =>
        AutomationProperties.GetName(stop) is { Length: > 0 } name ? name
        : stop is ButtonBase button && LabelOf(button) is { Length: > 0 } label ? label
        : (stop as FrameworkElement)?.ToolTip as string ?? stop.GetType().Name;
}
