namespace Memoria.ToDo.Shared.Tasks;

/// <summary>An image can't be shrunk to 1 MB.</summary>
public sealed class ImageTooLargeException : InvalidOperationException
{
}

/// <summary>The task isn't in <see cref="ITasks.All"/>, for example because it was deleted.</summary>
public sealed class TaskNotFoundException : InvalidOperationException
{
}
