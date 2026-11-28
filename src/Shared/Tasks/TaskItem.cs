namespace Memoria.ToDo.Shared.Tasks;

/// <summary>One task as the apps see it.</summary>
public sealed record TaskItem(string Id, string Title, string Description, string Rank,
    IReadOnlyList<string> Tags, DateTimeOffset Created, DateTimeOffset Updated, DateTimeOffset? Resolved)
{
    /// <summary>The tag that groups the task: <see cref="Tags"/>' first entry, or null when it has none.</summary>
    public string? GroupTag => Tags.Count == 0 ? null : Tags[0];
}
