namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Compares drafts by their text, since a draft holds a list.</summary>
internal sealed class DraftComparer : IEqualityComparer<TaskDraft?>
{
    public static readonly DraftComparer Instance = new();

    public bool Equals(TaskDraft? x, TaskDraft? y) =>
        x is not null && y is not null && x.TaskId == y.TaskId && x.Title == y.Title
        && x.Description == y.Description && x.Tags.SequenceEqual(y.Tags);

    public int GetHashCode(TaskDraft? obj) => obj?.TaskId.GetHashCode() ?? 0;
}
