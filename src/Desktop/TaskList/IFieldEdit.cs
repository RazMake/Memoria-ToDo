using System.Windows;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>One open edit of one field of a task, and what is particular to its kind: where it shows and how it ends.</summary>
internal interface IFieldEdit
{
    /// <summary>The editor control.</summary>
    FrameworkElement Editor { get; }

    /// <summary>The task as it was when the edit began.</summary>
    TaskItem Started { get; }

    /// <summary>True when the editor sits in the task's row on the left; false when it sits on the right side.</summary>
    bool ShowsInRow { get; }

    /// <summary>The tags the pills show for the edit: the started task's until the user changes them. They save with the edit and are dropped with it.</summary>
    IReadOnlyList<string> Tags { get; set; }

    /// <summary>True when the editor reports a change, or the <see cref="Tags"/> differ from the started ones in tags or order.</summary>
    bool HasChanges { get; }

    /// <summary>Saves the text and the <see cref="Tags"/> through Tasks in one save. False, with the edit open, when it can't be saved; the editor says why.</summary>
    bool Save();

    void FocusEnd();

    /// <summary>Ends the edit without saving. <paramref name="restore"/> brings it back, when the kind can be undone.</summary>
    void Leave(EditCleanup cleanup, Action restore);

    /// <summary>Ends the edit as the window closes, throwing away what it left.</summary>
    void Close(EditCleanup cleanup);
}
