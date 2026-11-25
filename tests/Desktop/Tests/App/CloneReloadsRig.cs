using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// What the clone reload tests share: <see cref="CloneReloads"/> over a fake watcher, a fake clock, a real synced repository
/// holding one part that counts its loads and notices, and a log. The repository has selected <see cref="Clone"/> when the rig is made.
/// </summary>
internal sealed class CloneReloadsRig : IDisposable
{
    public const string Clone = @"C:\Clones\one";
    public const string Other = @"C:\Clones\two";

    /// <summary>How long after the last change the reload waits.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(500);

    /// <summary>Makes the rig. A test that needs to see or change what <see cref="CloneReloads"/> asks of the repository passes <paramref name="decorate"/>, which wraps the real repository for it.</summary>
    public CloneReloadsRig(Func<SyncedRepository, ISyncedRepository>? decorate = null)
    {
        Repository = new SyncedRepository(Logs.CreateLogger());
        Repository.Add(Part);
        Repository.Select(Clone);
        Repository.LocalSaved += (_, _) => LocalSaves++;
        Reloads = new CloneReloads(Watcher, decorate?.Invoke(Repository) ?? Repository, Time, Logs.CreateLogger());
        ForgetLoads();
    }

    public FakeTimeProvider Time { get; } = new();
    public FakeFolderWatcher Watcher { get; } = new();
    public InMemoryLogSink Logs { get; } = new();
    public CountingPart Part { get; } = new();
    public SyncedRepository Repository { get; }
    public CloneReloads Reloads { get; }

    /// <summary>How many local saves the repository reported.</summary>
    public int LocalSaves { get; private set; }

    /// <summary>The loads the part made since the rig's setup or the latest <see cref="ForgetLoads"/>.</summary>
    public int Loads => Part.Loads - loadsBefore;

    /// <summary>The notices the part raised since the rig's setup or the latest <see cref="ForgetLoads"/>.</summary>
    public int Notices => Part.Notices - noticesBefore;

    private int loadsBefore;
    private int noticesBefore;

    /// <summary>Counts from now, such as after a selection the test made.</summary>
    public void ForgetLoads()
    {
        loadsBefore = Part.Loads;
        noticesBefore = Part.Notices;
    }

    public void Dispose() => Reloads.Dispose();

    /// <summary>A part that loads nothing and counts its successful loads and notices.</summary>
    internal sealed class CountingPart : IRepositoryPart
    {
        /// <summary>When set, <see cref="Load"/> throws it.</summary>
        public Exception? FailToLoad { get; set; }

        public int Loads { get; private set; }

        public int Notices { get; private set; }

        /// <summary>When set, <see cref="Loaded"/> throws it, after the load succeeded.</summary>
        public Exception? FailToNotify { get; set; }

        /// <summary>When set, runs at the start of every <see cref="Load"/>, with its folder, before it can fail or count.</summary>
        public Action<string?>? OnLoad { get; set; }

        public Action Load(string? folder)
        {
            OnLoad?.Invoke(folder);
            if (FailToLoad is { } failure)
                throw failure;
            Loads++;
            return () => { };
        }

        public void Loaded()
        {
            Notices++;
            if (FailToNotify is { } failure)
                throw failure;
        }

        public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles => [];
    }
}
