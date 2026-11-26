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

    /// <summary>
    /// Adds a Changed handler and a local save report handler that throw while <see cref="NoticeFailures.Active"/> is true, so a change Tasks
    /// saves then throws an <see cref="AggregateException"/> of "Changed failed." then "Report failed.". Call it before other handlers
    /// subscribe: after the first one throws, the later handlers of the same event don't run.
    /// </summary>
    public static NoticeFailures FailNotices(this ITasks tasks)
    {
        var failures = new NoticeFailures();
        tasks.Changed += (_, _) => failures.ThrowIfActive("Changed failed.");
        tasks.RepositoryOf().LocalSaved += (_, _) => failures.ThrowIfActive("Report failed.");
        return failures;
    }

    /// <summary>The switch <see cref="FailNotices"/> returns.</summary>
    public sealed class NoticeFailures
    {
        /// <summary>Whether the handlers throw now.</summary>
        public bool Active { get; set; }

        internal void ThrowIfActive(string message)
        {
            if (Active)
            {
                throw new IOException(message);
            }
        }
    }

    /// <summary>Selects the clone through the store's repository, so every part of it reads the folder; null clears the selection.</summary>
    public static void UseRepositoryFolder(this ITasks tasks, string? folder) => tasks.RepositoryOf().Select(folder);
}
