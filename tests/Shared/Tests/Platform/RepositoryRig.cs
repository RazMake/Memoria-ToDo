using Memoria.ToDo.Shared.Fakes;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>
/// A real <see cref="SyncedRepository"/> and the fake parts added to it, with one log of every step the parts see,
/// and a way to take the lock from another thread.
/// </summary>
internal sealed class RepositoryRig
{
    public const string Clone = "clone";

    private readonly List<string> log = [];

    public SyncedRepository Repository { get; } = new();

    /// <summary>Adds a part named <paramref name="name"/> that logs to the rig's log.</summary>
    public FakeRepositoryPart Add(string name)
    {
        var part = new FakeRepositoryPart(name, log);
        Repository.Add(part);
        return part;
    }

    /// <summary>Every step so far: the parts' own steps and the ones tests note.</summary>
    public List<string> Steps
    {
        get { lock (log) { return [.. log]; } }
    }

    public void Note(string step)
    {
        lock (log)
            log.Add(step);
    }

    public void ForgetSteps()
    {
        lock (log)
            log.Clear();
    }

    /// <summary>
    /// Starts another thread that takes the lock and then notes <paramref name="step"/>, and returns once that thread is
    /// about to ask for the lock. The returned task ends when the thread has had the lock.
    /// </summary>
    public Task LockOnAnotherThread(string step)
    {
        var asking = new ManualResetEventSlim();
        var task = Task.Run(() =>
        {
            asking.Set();
            Repository.Locked(_ => Note(step));
        });
        Assert.True(asking.Wait(TestWait.ForSignal));
        return task;
    }

    /// <summary>True when another thread can take the lock and finish with it now.</summary>
    public bool AnotherThreadCanLock() => Task.Run(() => Repository.Locked(_ => true)).Wait(TestWait.ForSignal);
}
