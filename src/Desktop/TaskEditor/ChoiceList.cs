namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The state of a list of strings the user picks from while typing: whether it is open, its items, and which one Down
/// moved to. A click, or Down then Enter, chooses an item and raises <see cref="Chosen"/> with its position.
/// </summary>
internal sealed class ChoiceList
{
    private IReadOnlyList<string> _items = [];

    public bool IsOpen { get; private set; }

    public IReadOnlyList<string> Items => _items;

    /// <summary>The position of the item Down moved to, or -1 when none.</summary>
    public int SelectedIndex { get; private set; } = -1;

    /// <summary>Raised with the position of the item the user chose.</summary>
    public event Action<int>? Chosen;

    /// <summary>Raised when the list is shown, moved in, or closed.</summary>
    public event Action? Changed;

    /// <summary>Shows the items with none chosen yet.</summary>
    public void Show(IReadOnlyList<string> items)
    {
        _items = items;
        SelectedIndex = -1;
        IsOpen = true;
        Changed?.Invoke();
    }

    /// <summary>Hides the list. Returns false, doing nothing, when it was not shown.</summary>
    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        IsOpen = false;
        _items = [];
        SelectedIndex = -1;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Moves to the next item, or the first when none is chosen; stays on the last.</summary>
    public void MoveDown()
    {
        SelectedIndex = Math.Min(SelectedIndex + 1, _items.Count - 1);
        Changed?.Invoke();
    }

    /// <summary>Chooses the item Down moved to; the list stays open. Returns false, doing nothing, when none was moved to.</summary>
    public bool ChooseMoved()
    {
        if (SelectedIndex < 0)
        {
            return false;
        }

        Chosen?.Invoke(SelectedIndex);
        return true;
    }

    /// <summary>Chooses the item at the position, as a click does.</summary>
    public void Choose(int position) => Chosen?.Invoke(position);
}
