using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Shared.Rendering;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// How a Task View test reaches the tag parts, through the ids the Task editor's tests already use (see <see cref="TagControls"/>).
/// The selected task's tag control shows on the right side, during an edit too: one pill ("TagPill {tag}") with its x
/// ("ClearTag {tag}") for each tag in order, the add pill ("No tag" while there's none, "+" after), and the menu they open. A
/// group heading shows its tag as one badge. Rows show no badges. Each helper that names a task's title needs that task to be
/// the selected one. Shared test setup.
/// </summary>
internal sealed partial class TaskListScreen
{
    /// <summary>Lets pending work finish, then lays the window out.</summary>
    public void Settle() => Refresh();

    /// <summary>The tags the selected task's pills show, in the order the pills show; empty when it shows only "No tag".</summary>
    public IReadOnlyList<string> PillTags(string title)
    {
        RequireSelected(title);
        return TagControls.PillTags(Details);
    }

    /// <summary>The words the selected task's add pill shows: "No tag" or "+".</summary>
    public string AddPillText(string title)
    {
        RequireSelected(title);
        return TagControls.AddPillText(Details);
    }

    /// <summary>Clicks the x of the pill of the tag on the selected task.</summary>
    public void ClearTag(string title, string tag)
    {
        RequireSelected(title);
        Activate(TagControls.ClearButton(Details, tag));
    }

    /// <summary>Clicks the pill of the tag on the selected task, which opens the menu that replaces it.</summary>
    public void OpenTagMenu(string title, string tag)
    {
        RequireSelected(title);
        Activate(TagControls.TagPill(Details, tag));
    }

    /// <summary>Clicks the selected task's add pill, which opens the menu that adds a tag.</summary>
    public void OpenAddTagMenu(string title)
    {
        RequireSelected(title);
        Activate(TagControls.AddPill(Details));
    }

    /// <summary>Whether the whole row of the title is inside the part of the left side that shows, so the user sees it without scrolling.</summary>
    public bool RowIsInView(string title, string? group = null)
    {
        var row = Row(title, group);
        Refresh();
        var scroller = Ancestors(row).OfType<System.Windows.Controls.ScrollViewer>().First();
        scroller.UpdateLayout();
        var top = row.TranslatePoint(new Point(0, 0), scroller).Y;
        return top >= -0.5 && top + row.ActualHeight <= scroller.ViewportHeight + 0.5;
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject element)
    {
        for (var parent = System.Windows.Media.VisualTreeHelper.GetParent(element); parent is not null;
             parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
        {
            yield return parent;
        }
    }

    /// <summary>The tags the open tag menu lists, in order.</summary>
    public IReadOnlyList<string> MenuTags
    {
        get
        {
            Refresh();
            return TagControls.MenuTags(Window);
        }
    }

    /// <summary>Chooses a tag from the open tag menu.</summary>
    public void ChooseMenuTag(string tag) => Activate(TagControls.MenuItem(Window, tag));

    /// <summary>Chooses Custom in the open tag menu.</summary>
    public void ChooseCustom() => Activate(TagControls.CustomItem(Window));

    /// <summary>Whether Custom's tag box shows.</summary>
    public bool CustomIsOpen
    {
        get
        {
            Refresh();
            return TagControls.CustomIsOpen(Window);
        }
    }

    /// <summary>Sets what the user has typed in Custom's tag box, as typing does.</summary>
    public void TypeTag(string text)
    {
        Refresh();
        EditorFields.Type(Window, EditorFields.Tags, text);
    }

    /// <summary>What Custom's tag box holds now.</summary>
    public string TypedTag
    {
        get
        {
            Refresh();
            return EditorFields.TextOf(Window, EditorFields.Tags);
        }
    }

    /// <summary>Gives the search box the keyboard focus, as clicking it does.</summary>
    public void FocusSearchBox()
    {
        Refresh();
        var box = Visuals(Window).OfType<System.Windows.Controls.TextBox>().Single(b => AutomationProperties.GetName(b) == "Search");
        box.Focus();
        Keyboard.Focus(box);
    }
}
