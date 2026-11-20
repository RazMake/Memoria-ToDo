using System.Runtime.CompilerServices;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Serilog;
using Serilog.Core;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// Tasks the way an app's composition root makes it: joined to a synced repository, which selects the folder.
/// A test that needs a task store creates it here, then selects a folder with <see cref="UseRepositoryFolder"/>,
/// which selects through that repository.
/// </summary>
public static class TestTaskStore
{
    private static readonly ConditionalWeakTable<ITasks, SyncedRepository> Repositories = new();

    /// <summary>
    /// Creates Tasks over <paramref name="repository"/>, or over a new repository when none is given. Both log to
    /// <paramref name="logger"/>, or to nothing when none is given.
    /// </summary>
    public static ITaskStore Create(IFileSystem files, IImageEncoder images, TimeProvider time, string deviceFolder, SyncedRepository? repository = null, ILogger? logger = null)
    {
        logger ??= Logger.None;
        repository ??= new SyncedRepository(logger);
        var store = TaskStore.Create(files, images, time, deviceFolder, repository, logger);
        Repositories.Add(store, repository);
        return store;
    }

    /// <summary>The repository the store was created over.</summary>
    public static SyncedRepository RepositoryOf(this ITasks tasks) =>
        Repositories.TryGetValue(tasks, out var repository)
            ? repository
            : throw new InvalidOperationException("The store wasn't created by TestTaskStore.");

    /// <summary>Selects the clone through the store's repository, so every part of it reads the folder; null clears the selection.</summary>
    public static void UseRepositoryFolder(this ITasks tasks, string? folder) => tasks.RepositoryOf().Select(folder);
}
