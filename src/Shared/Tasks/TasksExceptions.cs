namespace Memoria.ToDo.Shared.Tasks;

/// <summary>An image can't be shrunk to 1 MB.</summary>
public sealed class ImageTooLargeException : InvalidOperationException
{
}

/// <summary>The task changed since the caller read it. Current is the task as it is now.</summary>
public sealed class StaleTaskException : InvalidOperationException
{
    public StaleTaskException(TaskItem current) => Current = current;

    public TaskItem Current { get; }
}

/// <summary>The task isn't in <see cref="ITasks.All"/>, for example because it was deleted.</summary>
public sealed class TaskNotFoundException : InvalidOperationException
{
}
