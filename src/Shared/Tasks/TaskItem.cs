namespace Memoria.ToDo.Shared.Tasks;

/// <summary>One task as the apps see it.</summary>
public sealed record TaskItem(string Id, string Title, string Description, string Rank,
    IReadOnlyList<string> Tags, DateTimeOffset Created, DateTimeOffset Updated, DateTimeOffset? Resolved);
