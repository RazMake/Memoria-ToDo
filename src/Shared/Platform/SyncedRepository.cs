using System.Runtime.ExceptionServices;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// The one clone every store shares: its selected folder, the lock that runs one change at a time, the reload after
/// a sync, and the notice of a local save. Stores join as <see cref="IRepositoryPart"/>s before the first selection.
/// </summary>
public sealed class SyncedRepository : IRepositoryAccess, ISyncedRepository
{
    private readonly Lock gate = new();
    private readonly List<IRepositoryPart> parts = [];
    private string? folder;
    private bool selected;

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
            List<Exception>? failures = null;
            foreach (var part in parts)
            {
                try
                {
                    applies.Add(part.Load(folder));
                }
                catch (Exception e)
                {
                    (failures ??= []).Add(e);
                }
            }

            if (failures is not null)
                Throw(failures);
            foreach (var apply in applies)
                apply();
            this.folder = folder;
        }

        var noticeFailures = new List<Exception>();
        RaiseLoaded(parts, noticeFailures);
        if (noticeFailures.Count > 0)
            Throw(noticeFailures);
    }

    /// <inheritdoc />
    public void Update(string expectedFolder, Action update)
    {
        ArgumentNullException.ThrowIfNull(expectedFolder);
        ArgumentNullException.ThrowIfNull(update);
        RequireOutsideLock(nameof(Update));

        var failures = new List<Exception>();
        var reloaded = new List<IRepositoryPart>(parts.Count);
        lock (gate)
        {
            if (folder != expectedFolder)
                throw new InvalidOperationException("The selected repository folder isn't the expected one.");
            try
            {
                update();
            }
            catch (Exception e)
            {
                failures.Add(e);
            }

            foreach (var part in parts)
            {
                try
                {
                    part.Load(expectedFolder)();
                    reloaded.Add(part);
                }
                catch (Exception e)
                {
                    failures.Add(e);
                }
            }
        }

        RaiseLoaded(reloaded, failures);
        if (failures.Count > 0)
            Throw(failures);
    }

    /// <summary>Raises every part's notices, even when one throws, adding each failure in order.</summary>
    private static void RaiseLoaded(List<IRepositoryPart> loaded, List<Exception> failures)
    {
        foreach (var part in loaded)
        {
            try
            {
                part.Loaded();
            }
            catch (Exception e)
            {
                failures.Add(e);
            }
        }
    }

    private void RequireOutsideLock(string operation)
    {
        if (gate.IsHeldByCurrentThread)
            throw new InvalidOperationException($"{operation} can't run under the repository's lock.");
    }

    /// <summary>Throws the one failure as it is, or several together in order.</summary>
    private static void Throw(List<Exception> failures)
    {
        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException("More than one step failed.", failures);
    }
}
