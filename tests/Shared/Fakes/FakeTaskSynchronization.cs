using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>A skipped repository file for tests.</summary>
public sealed record FakeSkippedFile(string RelativePath, string Reason, bool RequiresUpdate = false) : ISkippedRepositoryFile;

/// <summary>
/// The shared fake of Sync's access to Tasks, for tests that don't run Git. Its layout rules are not used by Sync's
/// scheduling and setup, so they refuse every call.
/// </summary>
public sealed class FakeTaskSynchronization(SyncedRepository repository, FakeRepositoryPart part) : ITaskSynchronization
{
    /// <summary>Test shortcut: the folder the synced repository has selected; setting it selects that folder.</summary>
    public string? RepositoryFolder
    {
        get => repository.Folder;
        set => repository.Select(value);
    }

    /// <summary>Test shortcut: the skipped files the repository's one fake part reports.</summary>
    public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles
    {
        get => part.SkippedFiles;
        set => part.SkippedFiles = value;
    }

    /// <summary>Test shortcut: reports a local save to the synced repository, as Tasks does after a saved edit.</summary>
    public void RaiseLocalSaved() => repository.ReportLocalSave();

    /// <inheritdoc />
    public IRepositoryFileRules Files { get; } = new UnusedRules();

    /// <inheritdoc />
    public void EnsureSettings()
    {
    }

    /// <inheritdoc />
    public void DeleteExpiredTasks()
    {
    }

    private sealed class UnusedRules : IRepositoryFileRules
    {
        public string TasksFolderName => "tasks";
        public string ImagesFolderName => "images";
        public string SettingsFileName => "settings.json";
        public string TagColorsFileName => "tags.json";

        public RepositoryFileKind Classify(string relativePath) => throw new NotSupportedException();

        public string? TaskIdInPath(string relativePath) => throw new NotSupportedException();

        public byte[]? MergeTask(string expectedTaskId, byte[]? basis, byte[]? local, byte[]? remote) => throw new NotSupportedException();

        public byte[]? MergeSettings(byte[]? basis, byte[]? local, byte[]? remote) => throw new NotSupportedException();

        public byte[]? MergeTagColors(byte[]? basis, byte[]? local, byte[]? remote) => throw new NotSupportedException();
    }
}

/// <summary>The shared fake selected-folder owner: records each selection and makes it current in the synced repository, as the real adapter does.</summary>
public sealed class FakeRepositorySelection(SyncedRepository repository) : IRepositorySelection
{
    private readonly List<string> selected = [];

    /// <summary>Every folder selected so far, in order.</summary>
    public IReadOnlyList<string> Selected
    {
        get { lock (selected) { return [.. selected]; } }
    }

    /// <summary>When set, the next selection fails with it, selects nothing, and the failure is then forgotten.</summary>
    public Exception? NextSelectFailure { get; set; }

    /// <summary>When set, loading the saved selection fails with it.</summary>
    public Exception? LoadFailure { get; set; }

    /// <summary>How many times the saved selection was loaded.</summary>
    public int LoadCalls => Volatile.Read(ref loadCalls);

    private int loadCalls;

    /// <summary>Counts the load, and fails it when <see cref="LoadFailure"/> is set.</summary>
    public void Load()
    {
        Interlocked.Increment(ref loadCalls);
        if (LoadFailure is { } failure)
            throw failure;
    }

    /// <inheritdoc />
    public void Select(string folder)
    {
        if (NextSelectFailure is { } failure)
        {
            NextSelectFailure = null;
            throw failure;
        }

        repository.Select(folder);
        lock (selected) { selected.Add(folder); }
    }
}
