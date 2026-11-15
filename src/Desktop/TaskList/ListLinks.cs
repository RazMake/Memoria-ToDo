using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What following a link in the list asks the window to do.</summary>
internal sealed record LinkOutcome(LinkOutcome.Kind What, string? TaskId = null)
{
    public enum Kind { Ignored, Gone, Show }

    /// <summary>The address is not a task link: nothing to do.</summary>
    public static LinkOutcome Ignored { get; } = new(Kind.Ignored);

    /// <summary>The task no longer exists, or waits on the Undo bar to be deleted.</summary>
    public static LinkOutcome Gone { get; } = new(Kind.Gone);

    /// <summary>Show and open the task with this id.</summary>
    public static LinkOutcome Show(string id) => new(Kind.Show, id);
}

/// <summary>Following a link to a task: the list is made to show it by clearing only what hides it.</summary>
internal sealed class ListLinks(ITasks tasks, ListFilter filter, ListContent content)
{
    public LinkOutcome Follow(string address)
    {
        if (tasks.TaskIdIn(address) is not { } id)
        {
            return LinkOutcome.Ignored;
        }

        var target = tasks.All.FirstOrDefault(task => task.Id == id);
        if (target is null || id == content.HiddenId)
        {
            return LinkOutcome.Gone;
        }

        // Clear only what hides the task, keeping the rest of the filter.
        if (filter.SearchText.Length > 0 && tasks.Search(filter.SearchText).All(task => task.Id != id))
        {
            filter.ClearSearch();
        }

        if (filter.TagFilter is not null && filter.Matching(tasks).All(task => task.Id != id))
        {
            filter.ClearTag();
        }

        if (target.Resolved is not null)
        {
            content.ShowResolved = true;
        }

        return LinkOutcome.Show(id);
    }
}
