namespace Memoria.ToDo.Shared.Tasks;

/// <summary>This device's draft of a new task. <paramref name="TaskId"/> is the ID the task gets when it's added.</summary>
public sealed record TaskDraft(string TaskId, string Title, string Description, IReadOnlyList<string> Tags);
