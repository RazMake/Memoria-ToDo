namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Which of a fixed number of sections is open: exactly one always is, the first at first. Selecting the open one
/// changes nothing, and no section can be closed on its own.
/// </summary>
internal sealed class AccordionState
{
    private readonly int _count;

    public AccordionState(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        _count = count;
    }

    /// <summary>The index of the open section.</summary>
    public int Open { get; private set; }

    /// <summary>Opens the section and closes the open one. Returns false, changing nothing, when it is the open one.</summary>
    public bool Select(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
        if (index == Open)
        {
            return false;
        }

        Open = index;
        return true;
    }
}
