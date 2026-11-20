using System.Windows.Input;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The popup lists of one editor, of which at most one is open. Down, Up, and Enter act on the open one.</summary>
internal sealed class PopupLists
{
    private readonly IReadOnlyList<ChoiceList> _all;
    private readonly HashSet<ChoiceList> _typedLists = [];

    public PopupLists(params ChoiceList[] lists) => _all = lists;

    /// <summary>Shows the items in the list, closing whichever list was open.</summary>
    public void Show(ChoiceList list, IReadOnlyList<string> items)
    {
        CloseOpen();
        list.Show(items);
    }

    /// <summary>
    /// Follows the field's edits so that a list opened by typed text (<paramref name="own"/>) can tell whether another list opened
    /// by typed text was open before that text changed anything: the text is that list's filter, and opens no list.
    /// </summary>
    /// <returns>Whether such a list was open when the last edit began.</returns>
    public Func<bool> WatchOtherTypedListOpenAtEdits(ITextField field, ChoiceList own)
    {
        _typedLists.Add(own);
        var otherWasOpen = false;
        field.Document.Changing += (_, _) => otherWasOpen = _typedLists.Any(list => list != own && list.IsOpen);
        return () => otherWasOpen;
    }

    /// <summary>Closes the open list. Returns false, doing nothing, when none was open.</summary>
    public bool CloseOpen()
    {
        var open = Open();
        return open?.Close() ?? false;
    }

    /// <summary>Moves to the next item of the open list. Returns false, doing nothing, when none is open.</summary>
    public bool MoveDown()
    {
        if (Open() is not { } open)
        {
            return false;
        }

        open.MoveDown();
        return true;
    }

    /// <summary>Moves to the item before in the open list. Returns false, doing nothing, when none is open.</summary>
    public bool MoveUp()
    {
        if (Open() is not { } open)
        {
            return false;
        }

        open.MoveUp();
        return true;
    }

    /// <summary>Adds Down, Up, and Enter, which act on the open list and keep their usual meaning when none is open.</summary>
    public KeyMap AddKeysTo(KeyMap keys) => keys
        .Try(ModifierKeys.None, Key.Down, MoveDown)
        .Try(ModifierKeys.None, Key.Up, MoveUp)
        .Try(ModifierKeys.None, Key.Enter, ChooseMovedOrClose);

    /// <summary>
    /// Chooses the item Down moved to in the open list. When the list is open and nothing was moved to, closes it and
    /// returns false so that Enter does what it does with no list. Returns false, doing nothing, when none is open.
    /// </summary>
    public bool ChooseMovedOrClose()
    {
        if (Open() is not { } open)
        {
            return false;
        }

        if (open.ChooseMoved())
        {
            return true;
        }

        open.Close();
        return false;
    }

    private ChoiceList? Open() => _all.FirstOrDefault(list => list.IsOpen);
}
