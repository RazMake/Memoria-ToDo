using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// In-memory <see cref="ISharedTaskSettings"/>. Set <see cref="ReadFailure"/> to make reads throw, as an unreadable
/// file does, and <see cref="SetFailure"/> to make the setters throw. Like the real settings, a setter that changes
/// nothing raises no <see cref="Changed"/>; use <see cref="RaiseChanged"/> to model a reload.
/// </summary>
public sealed class FakeSharedTaskSettings : ISharedTaskSettings
{
    private int deletionDelayDays = 7;
    private string? addNewTaskShortcut;
    private string? manageTasksShortcut;
    private string? closeWindowShortcut;
    private string? saveShortcut;
    private string? configureShortcut;
    private string? contactsShortcut;

    /// <summary>When set, every read throws it.</summary>
    public Exception? ReadFailure { get; set; }

    /// <summary>When set, every setter throws it and saves nothing.</summary>
    public Exception? SetFailure { get; set; }

    /// <summary>The delays the setters accepted, in order.</summary>
    public List<int> SavedDelays { get; } = [];

    /// <inheritdoc />
    public int DeletionDelayDays => Read(deletionDelayDays);

    /// <inheritdoc />
    public string? AddNewTaskShortcut => Read(addNewTaskShortcut);

    /// <inheritdoc />
    public string? ManageTasksShortcut => Read(manageTasksShortcut);

    /// <inheritdoc />
    public string? CloseWindowShortcut => Read(closeWindowShortcut);

    /// <inheritdoc />
    public string? SaveShortcut => Read(saveShortcut);

    /// <inheritdoc />
    public string? ConfigureShortcut => Read(configureShortcut);

    /// <inheritdoc />
    public string? ContactsShortcut => Read(contactsShortcut);

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void SetDeletionDelayDays(int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, 365);
        Write(ref deletionDelayDays, days);
        SavedDelays.Add(days);
    }

    /// <inheritdoc />
    public void SetAddNewTaskShortcut(string? shortcut) => Write(ref addNewTaskShortcut, shortcut);

    /// <inheritdoc />
    public void SetManageTasksShortcut(string? shortcut) => Write(ref manageTasksShortcut, shortcut);

    /// <inheritdoc />
    public void SetCloseWindowShortcut(string? shortcut) => Write(ref closeWindowShortcut, shortcut);

    /// <inheritdoc />
    public void SetSaveShortcut(string? shortcut) => Write(ref saveShortcut, shortcut);

    /// <inheritdoc />
    public void SetConfigureShortcut(string? shortcut) => Write(ref configureShortcut, shortcut);

    /// <inheritdoc />
    public void SetContactsShortcut(string? shortcut) => Write(ref contactsShortcut, shortcut);

    /// <summary>Raises <see cref="Changed"/>, as a reload does.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private T Read<T>(T value) => ReadFailure is null ? value : throw ReadFailure;

    private void Write<T>(ref T field, T value)
    {
        if (SetFailure is not null)
        {
            throw SetFailure;
        }

        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            RaiseChanged();
        }
    }
}
