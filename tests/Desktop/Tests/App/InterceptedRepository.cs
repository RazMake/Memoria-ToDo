using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>A repository that runs a test's action at the start of each <see cref="Update"/>, then lets the real repository handle it.</summary>
internal sealed class InterceptedRepository(ISyncedRepository inner, Action<string> beforeUpdate) : ISyncedRepository
{
    public string? Folder => inner.Folder;

    public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles => inner.SkippedFiles;

    public event EventHandler? LocalSaved
    {
        add => inner.LocalSaved += value;
        remove => inner.LocalSaved -= value;
    }

    public void Update(string expectedFolder, Action update)
    {
        beforeUpdate(expectedFolder);
        inner.Update(expectedFolder, update);
    }
}
