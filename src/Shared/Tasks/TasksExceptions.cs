namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Adding a task needs this device's clone folder, and none is set.</summary>
public sealed class NoRepositoryFolderException : InvalidOperationException
{
}

/// <summary>An image can't be shrunk to 1 MB.</summary>
public sealed class ImageTooLargeException : InvalidOperationException
{
}
