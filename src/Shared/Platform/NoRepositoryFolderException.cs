namespace Memoria.ToDo.Shared.Platform;

/// <summary>A store needs this device's clone folder, and none is set.</summary>
public sealed class NoRepositoryFolderException : InvalidOperationException
{
}
