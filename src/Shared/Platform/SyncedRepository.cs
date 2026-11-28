using Serilog;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// The one clone every store shares: its selected folder, the lock that runs one change at a time, the reload after
/// a sync, and the notice of a local save. Stores join as <see cref="IRepositoryPart"/>s before the first selection.
/// </summary>
public sealed class SyncedRepository : IRepositoryAccess, ISyncedRepository
{
    private readonly Lock gate = new();
    private readonly List<IRepositoryPart> parts = [];
    private readonly ILogger logger;
    private string? folder;
    private bool selected;

    /// <summary>Logs each load of the clone, and each file a part skipped, to <paramref name="logger"/>. Throws <see cref="ArgumentNullException"/> for null.</summary>
    public SyncedRepository(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler? LocalSaved;

    /// <inheritdoc />
    public string? Folder => Locked(selectedFolder => selectedFolder);

    /// <inheritdoc />
    public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles
    {
        get
        {
            lock (gate)
                return [.. parts.SelectMany(part => part.SkippedFiles)];
        }
    }

    /// <inheritdoc />
    public void Add(IRepositoryPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        lock (gate)
        {
            if (selected)
                throw new InvalidOperationException("A part can't be added after the first selection.");
            if (parts.Contains(part))
                throw new ArgumentException("The part was already added.", nameof(part));
            parts.Add(part);
        }
    }

    /// <inheritdoc />
    public T Locked<T>(Func<string?, T> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        lock (gate)
            return call(folder);
    }

    /// <inheritdoc />
    public void Locked(Action<string?> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        lock (gate)
            call(folder);
    }

    /// <inheritdoc />
    public T LockedChange<T>(Func<string?, T> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        RequireOutsideLock(nameof(LockedChange));
        return Locked(call);
    }

    /// <inheritdoc />
    public void LockedChange(Action<string?> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        RequireOutsideLock(nameof(LockedChange));
        Locked(call);
    }

    /// <inheritdoc />
    public void ReportLocalSave()
    {
        RequireOutsideLock(nameof(ReportLocalSave));
        LocalSaved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Loads every part with the folder, or empties them for null, then raises their notices.
    /// Throws, before loading: <see cref="ArgumentException"/> for a blank folder; <see cref="InvalidOperationException"/> under the lock.
    /// Throws the part's exception, or <see cref="AggregateException"/> when several fail. When the reads succeed,
    /// every part raises its notices; failed notices are thrown the same way, in the order parts were added.
    /// </summary>
    public void Select(string? folder)
    {
        if (folder is not null && string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Must be a folder path, or null.", nameof(folder));
        RequireOutsideLock(nameof(Select));

        lock (gate)
        {
            selected = true;
            var applies = new List<Action>(parts.Count);
            var failures = new FailureList();
            foreach (var part in parts)
                failures.Attempt(() => applies.Add(part.Load(folder)));

            failures.ThrowIfAny();
            foreach (var apply in applies)
                apply();
            this.folder = folder;
            if (folder is not null)
                LogLoad(folder, parts);
        }

        var noticeFailures = new FailureList();
        RaiseLoaded(parts, noticeFailures);
        noticeFailures.ThrowIfAny();
    }

    /// <inheritdoc />
    public void Update(string expectedFolder, Action update)
    {
        ArgumentNullException.ThrowIfNull(expectedFolder);
        ArgumentNullException.ThrowIfNull(update);
        RequireOutsideLock(nameof(Update));

        var failures = new FailureList();
        var reloaded = new List<IRepositoryPart>(parts.Count);
        lock (gate)
        {
            if (folder != expectedFolder)
                throw new InvalidOperationException("The selected repository folder isn't the expected one.");
            failures.Attempt(update);
            foreach (var part in parts)
            {
                failures.Attempt(() =>
                {
                    part.Load(expectedFolder)();
                    reloaded.Add(part);
                });
            }

            if (reloaded.Count > 0)
                LogLoad(expectedFolder, reloaded);
        }

        RaiseLoaded(reloaded, failures);
        failures.ThrowIfAny();
    }

    /// <summary>Logs a load: how many files each part skipped, then each skipped file. Call under the lock.</summary>
    private void LogLoad(string loadedFolder, List<IRepositoryPart> loaded)
    {
        var counts = loaded.Select(part => part.SkippedFiles.Count).ToArray();
        logger.Information("Loaded the repository folder {Folder}; files skipped by each store: {SkippedCounts}", loadedFolder, counts);
        foreach (var file in loaded.SelectMany(part => part.SkippedFiles))
        {
            logger.Warning(
                "Skipped {Path}: {Reason} (needs a newer app: {RequiresUpdate})",
                file.RelativePath, file.Reason, file.RequiresUpdate);
        }
    }

    /// <summary>Raises every part's notices, even when one throws, keeping each failure in order.</summary>
    private static void RaiseLoaded(List<IRepositoryPart> loaded, FailureList failures)
    {
        foreach (var part in loaded)
            failures.Attempt(part.Loaded);
    }

    private void RequireOutsideLock(string operation)
    {
        if (gate.IsHeldByCurrentThread)
            throw new InvalidOperationException($"{operation} can't run under the repository's lock.");
    }
}
