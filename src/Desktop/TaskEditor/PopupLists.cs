namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The popup lists of one editor, of which at most one is open. Down and Enter act on the open one.</summary>
internal sealed class PopupLists
{
    private readonly IReadOnlyList<PopupList> _all;

    public PopupLists(params PopupList[] lists) => _all = lists;

    /// <summary>Shows the items in the list, closing whichever list was open.</summary>
    public void Show(PopupList list, IReadOnlyList<string> items)
    {
        CloseOpen();
        list.Show(items);
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

    private PopupList? Open() => _all.FirstOrDefault(list => list.IsOpen);
}
