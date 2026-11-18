namespace Memoria.ToDo.Shared.Platform;

/// <summary>Marks an <see cref="InvalidDataException"/> as caused by a <c>version</c> newer than this app reads, so only a newer app can read the file.</summary>
internal sealed class NewerVersionException() : Exception("The file needs a newer version of the app.");
