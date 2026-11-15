using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// How a View Tasks test reaches the tag parts, through the ids the Task editor's tests already use (see <see cref="TagControls"/>).
/// A row shows its tags as badges, elements with id "Badge {tag}", so a collapsed row's badges are its tags. The tag control of an
/// expanded task, and the editor's own during an edit, both show remove buttons ("Remove {tag}") and the + button ("AddTag"), so
/// whether a row shows a tag control is whether it shows the + button. Shared test setup.
/// </summary>
internal sealed partial class TaskListScreen
{
    /// <summary>Lets pending work finish, then lays the window out.</summary>
    public void Settle() => Refresh();

    /// <summary>The tags of the badges the row shows, in order.</summary>
    public IReadOnlyList<string> BadgeTags(string title) => TagControls.BadgeTags(Row(title));

    /// <summary>The tags whose remove buttons the row's tag control shows, in order.</summary>
    public IReadOnlyList<string> ControlTags(string title) => TagControls.RemovableTags(Row(title));

    /// <summary>Whether the row shows a tag control: the one an expanded task has, or the editor's own during an edit.</summary>
    public bool ShowsTagControl(string title) => HasTagControl(Row(title));

    /// <summary>How many tag controls show in the whole window.</summary>
    public int TagControlCount
    {
        get
        {
            Refresh();
            return Rows().Count(HasTagControl);
        }
    }

    /// <summary>Clicks the remove button of the tag in the row's tag control.</summary>
    public void RemoveTag(string title, string tag) => Activate(TagControls.RemoveButton(Row(title), tag));

    /// <summary>Clicks the + button of the row's tag control, which opens its menu.</summary>
    public void OpenPlusMenu(string title) => Activate(TagControls.Plus(Row(title)));

    /// <summary>Whether the + menu is open anywhere in the window.</summary>
    public bool PlusMenuIsOpen
    {
        get
        {
            Refresh();
            return TagControls.MenuIsOpen(Window);
        }
    }

    /// <summary>The tags the open + menu lists, in order.</summary>
    public IReadOnlyList<string> MenuTags
    {
        get
        {
            Refresh();
            return TagControls.MenuTags(Window);
        }
    }

    /// <summary>Chooses a tag from the open + menu.</summary>
    public void ChooseMenuTag(string tag) => Activate(TagControls.MenuItem(Window, tag));

    /// <summary>Chooses Custom in the open + menu.</summary>
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

    /// <summary>Every tag the "All tags" list offers, in order.</summary>
    public IReadOnlyList<string> AllTags => [.. AllTagsList().Items.Cast<string>()];

    /// <summary>Gives the tag's chip in the filter the keyboard focus, as tabbing to a header button does.</summary>
    public void FocusChip(string tag)
    {
        var chip = Buttons(Named("Tag filter")).Single(b => LabelOf(b) == tag);
        chip.Focus();
        Keyboard.Focus(chip);
    }

    /// <summary>Gives the search box the keyboard focus, as clicking it does.</summary>
    public void FocusSearchBox()
    {
        Refresh();
        var box = Visuals(Window).OfType<System.Windows.Controls.TextBox>().Single(b => AutomationProperties.GetName(b) == "Search");
        box.Focus();
        Keyboard.Focus(box);
    }

    /// <summary>The fill of the tag's badge in the open "All tags" list, found by the badge's automation id, whatever the list's items are.</summary>
    public TagFill AllTagsBadgeFill(string tag)
    {
        var all = Visuals(Window).OfType<System.Windows.Controls.ComboBox>()
            .Single(c => c.IsVisible && AutomationProperties.GetName(c) == "All tags");
        Refresh();
        var item = Enumerable.Range(0, all.Items.Count)
            .Select(all.ItemContainerGenerator.ContainerFromIndex)
            .OfType<DependencyObject>()
            .Single(container => TagControls.BadgeTags(container).Contains(tag));
        return TagControls.FillOf(item, tag);
    }

    private static bool HasTagControl(FrameworkElement row) =>
        TextFields.FindShowing(row, TagControls.AddTag) is not null;
}
