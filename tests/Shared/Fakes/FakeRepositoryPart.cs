using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// A repository part for tests of the synced repository and of Sync. It records each step the repository takes
/// ("name.Load(folder)", "name.Apply", "name.Loaded") in a log that several parts can share, and lets a test fail a
/// read, run code inside a step, and set the skipped files it reports.
/// </summary>
public sealed class FakeRepositoryPart(string name = "part", List<string>? log = null) : IRepositoryPart
{
    private readonly List<string> steps = log ?? [];
    private readonly List<string?> readFolders = [];
    private int applies;
    private int notices;

    /// <summary>When set, every read fails with it, before it makes anything current.</summary>
    public Exception? LoadFailure { get; set; }

    /// <summary>Runs in each read, with the folder, before <see cref="LoadFailure"/> is thrown.</summary>
    public Action<string?>? WhenReading { get; set; }

    /// <summary>Runs in each call that makes what a read found current.</summary>
    public Action? WhenApplying { get; set; }

    /// <summary>Runs in each notice, <see cref="Loaded"/>.</summary>
    public Action? WhenLoaded { get; set; }

    /// <summary>What the part reports as skipped.</summary>
    public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; set; } = [];

    /// <summary>The folder of every read, in order.</summary>
    public IReadOnlyList<string?> ReadFolders
    {
        get { lock (steps) { return [.. readFolders]; } }
    }

    /// <summary>How many times what a read found was made current.</summary>
    public int Applies => Volatile.Read(ref applies);

    /// <summary>How many notices were raised.</summary>
    public int Notices => Volatile.Read(ref notices);

    /// <summary>Every step of every part sharing this log, in order.</summary>
    public IReadOnlyList<string> Steps
    {
        get { lock (steps) { return [.. steps]; } }
    }

    /// <inheritdoc />
    public Action Load(string? folder)
    {
        lock (steps)
        {
            steps.Add($"{name}.Load({folder ?? "null"})");
            readFolders.Add(folder);
        }

        WhenReading?.Invoke(folder);
        if (LoadFailure is { } failure)
            throw failure;
        return () =>
        {
            lock (steps)
                steps.Add($"{name}.Apply");
            Interlocked.Increment(ref applies);
            WhenApplying?.Invoke();
        };
    }

    /// <inheritdoc />
    public void Loaded()
    {
        lock (steps)
            steps.Add($"{name}.Loaded");
        Interlocked.Increment(ref notices);
        WhenLoaded?.Invoke();
    }
}
