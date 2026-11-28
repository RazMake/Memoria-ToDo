namespace Memoria.ToDo.Desktop.TaskList.Tests;

// How a test types "@" in the search box and reads the contacts its list offers.
internal sealed partial class TaskListScreen
{
    /// <summary>Gives the search box the focus and types the text into it one character at a time, as the keyboard does; "@" opens the contact list.</summary>
    public void TypeInSearch(string text)
    {
        FocusSearchBox();
        TaskEditor.Tests.EditorFields.TypeOnFocused(text);
        Refresh();
    }

    /// <summary>The contacts the search box's open "@" list offers, in order; empty while it is closed.</summary>
    public IReadOnlyList<string> ShownAliases => TaskEditor.Tests.EditorFields.ShownPopupMentions();
}
