using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The tasks the list shows now: the open ones, and the resolved ones the filter leaves.</summary>
internal sealed record ListSnapshot(IReadOnlyList<TaskItem> Open, IReadOnlyList<TaskItem> Resolved, bool ShowResolved)
{
    /// <summary>The tasks that have rows: the open ones, then the resolved ones when they are chosen.</summary>
    public IReadOnlyList<TaskItem> Shown => ShowResolved ? [.. Open, .. Resolved] : Open;

    public int ResolvedCount => Resolved.Count;
}

/// <summary>What the list shows: the tasks the filter leaves, without the one waiting on the Undo bar.</summary>
internal sealed class ListContent(ITasks tasks, ListFilter filter)
{
    /// <summary>The task waiting on the Undo bar to be deleted, which the list leaves out, or null.</summary>
    public string? HiddenId { get; set; }

    /// <summary>Whether the resolved tasks show after the open ones.</summary>
    public bool ShowResolved { get; set; }

    /// <summary>
    /// Sorts what the filter leaves into open and resolved. The task being edited stays where <paramref name="previous"/> had it,
    /// with its text, even when it is gone, filtered out, or resolved elsewhere: a resolved row stays a resolved row, and an open
    /// row stays open.
    /// </summary>
    public ListSnapshot Build(TaskItem? editing, ListSnapshot? previous)
    {
        var open = new List<TaskItem>();
        var resolved = new List<TaskItem>();
        var editedResolvedRow = editing is not null && previous is not null && IndexOf(previous.Resolved, editing.Id) >= 0;
        var matching = filter.Matching(tasks).Where(task => task.Id != HiddenId).ToList();
        foreach (var task in matching)
        {
            (task.Resolved is null || (task.Id == editing?.Id && !editedResolvedRow) ? open : resolved).Add(task);
        }

        if (editing is not null && matching.All(task => task.Id != editing.Id))
        {
            var kept = editedResolvedRow ? resolved : open;
            var at = previous is null ? -1 : IndexOf(editedResolvedRow ? previous.Resolved : previous.Open, editing.Id);
            kept.Insert(at < 0 ? kept.Count : Math.Min(at, kept.Count), editing);
        }

        return new ListSnapshot(open, resolved, ShowResolved);
    }

    private static int IndexOf(IReadOnlyList<TaskItem> list, string id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
