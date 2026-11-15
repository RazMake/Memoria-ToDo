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

    bool HasChanges { get; }

    /// <summary>Saves through Tasks. False, with the edit open, when it can't be saved; the editor says why.</summary>
    bool Save();

    void FocusEnd();

    /// <summary>Ends the edit without saving. <paramref name="restore"/> brings it back, when the kind can be undone.</summary>
    void Leave(EditCleanup cleanup, Action restore);

    /// <summary>Ends the edit as the window closes, throwing away what it left.</summary>
    void Close(EditCleanup cleanup);
}
