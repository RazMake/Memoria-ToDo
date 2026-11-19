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
/// The selected task's tag control shows on the right side, during an edit too: one pill ("TagPill") with the tag or "No tag",
/// its x ("ClearTag") while there's a tag, and the menu it opens. A group heading shows its tag as one badge. Rows show no
/// badges. Each helper that names a task's title needs that task to be the selected one. Shared test setup.
/// </summary>
internal sealed partial class TaskListScreen
{
    /// <summary>Lets pending work finish, then lays the window out.</summary>
    public void Settle() => Refresh();

    /// <summary>The tags of the badges in the group headings, top to bottom.</summary>
    public IReadOnlyList<string> HeadingBadgeTags =>
        [.. Headings().OrderBy(PositionFromTop).SelectMany(heading => TagControls.BadgeTags(heading))];

    /// <summary>Whether any row shows a tag badge.</summary>
    public bool RowsShowBadges => Rows().Any(row => TagControls.BadgeTags(row).Count > 0);

    /// <summary>The tag the selected task's pill shows, or null when it shows "No tag".</summary>
    public string? PillTag(string title)
    {
        RequireSelected(title);
        return TagControls.PillTag(Details);
    }

    /// <summary>The words the selected task's pill shows: the tag, or "No tag".</summary>
    public string PillText(string title)
    {
        RequireSelected(title);
        return TagControls.PillText(Details);
    }

    /// <summary>Whether the selected task's pill shows an x.</summary>
    public bool PillHasClear(string title)
    {
        RequireSelected(title);
        return TagControls.HasClearButton(Details);
    }

    /// <summary>Whether the right side shows a tag control: the selected task's, during an edit too.</summary>
    public bool ShowsTagControl(string title) => SelectedTitles.Contains(title) && HasTagControl(Details);

    /// <summary>How many tag controls show in the whole window: one on the right side, or none.</summary>
    public int TagControlCount
    {
        get
        {
            Refresh();
            return HasTagControl(Details) ? 1 : 0;
        }
    }

    /// <summary>Clicks the x of the selected task's pill.</summary>
    public void ClearTag(string title)
    {
        RequireSelected(title);
        Activate(TagControls.ClearButton(Details));
    }

    /// <summary>Clicks the selected task's pill, which opens its menu.</summary>
    public void OpenTagMenu(string title)
    {
        RequireSelected(title);
        Activate(TagControls.PillButton(Details));
    }

    /// <summary>Gives the selected task's pill the keyboard focus, as clicking it or tabbing to it does, without opening its menu.</summary>
    public void FocusTagPill(string title)
    {
        RequireSelected(title);
        var pill = TagControls.PillButton(Details);
        pill.Focus();
        Keyboard.Focus(pill);
    }

    /// <summary>Whether the tag menu is open anywhere in the window.</summary>
    public bool TagMenuIsOpen
    {
        get
        {
            Refresh();
            return TagControls.MenuIsOpen(Window);
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

    /// <summary>Gives the search box the keyboard focus, as clicking it does.</summary>
    public void FocusSearchBox()
    {
        Refresh();
        var box = Visuals(Window).OfType<System.Windows.Controls.TextBox>().Single(b => AutomationProperties.GetName(b) == "Search");
        box.Focus();
        Keyboard.Focus(box);
    }

    /// <summary>What Custom's tag box holds: the tag text typed and not yet chosen.</summary>
    public string EditedTagsText => TextFields.TextOf(CustomBox());

    /// <summary>Types the text into Custom's tag box without choosing it as the tag, and gives the box the focus. The box must be open.</summary>
    public void TypeTags(string text)
    {
        var field = CustomBox();
        TextFields.Type(field, text);
        field.Focus();
        Keyboard.Focus(field);
    }

    private FrameworkElement CustomBox()
    {
        Refresh();
        return TextFields.FindShowing(Window, EditorFields.Tags) ?? throw new InvalidOperationException("Custom's tag box isn't showing.");
    }

    private static bool HasTagControl(FrameworkElement row) =>
        TextFields.FindShowing(row, TagControls.Pill) is not null;
}
