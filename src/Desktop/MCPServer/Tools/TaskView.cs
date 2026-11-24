using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.MCPServer.Tools;

/// <summary>A task as the assistant sees it.</summary>
internal static class TaskView
{
    /// <summary>The task's fields; the description only when <paramref name="withDescription"/>, since lists stay short without it.</summary>
    public static object Of(TaskItem task, bool withDescription) => new
    {
        id = task.Id,
        title = task.Title,
        description = withDescription ? task.Description : null,
        tags = task.Tags,
        created = task.Created,
        updated = task.Updated,
        resolved = task.Resolved,
    };

    /// <summary>The tasks as <c>{ tasks: [...] }</c>, in the order given.</summary>
    public static object ListOf(IEnumerable<TaskItem> tasks, bool withDescription) =>
        new { tasks = tasks.Select(task => Of(task, withDescription)).ToList() };
}
