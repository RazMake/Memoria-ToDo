using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// The shared fake of Sync's Git operations, with no files, network, or native library. An unscripted pass succeeds
/// at once. A test holds a pass with <see cref="ScriptPass"/> and ends it when the scenario needs, so no test sleeps.
/// </summary>
internal sealed class FakeGitOperations : IGitOperations
{
    private readonly object gate = new();
    private readonly Dictionary<string, GitReceipt> receipts = [];
    private readonly Dictionary<string, SshAddress> origins = [];
    private readonly Queue<PassScript> scripts = new();
    private readonly List<(int Count, TaskCompletionSource Reached)> passWaiters = [];
    private readonly List<SshAddress> cloned = [];
    private readonly List<SshAddress> reused = [];
    private int passesStarted;
    private int activePasses;
    private int maxActivePasses;
    private int connectionsOpened;
    private int originReads;

    /// <summary>The result of every pass without a script.</summary>
    public GitPassResult DefaultResult { get; set; } = new(false);

    /// <summary>When set, the next clone or pass offers this host key to Sync's verifier, as the transport does.</summary>
    public (string Algorithm, string Fingerprint)? OfferedHostKey { get; set; }

    /// <summary>When set, the next clone fails with it, and the failure is then forgotten.</summary>
    public Exception? NextCloneFailure { get; set; }

    /// <summary>When set, the next use of an existing clone fails with it, and the failure is then forgotten.</summary>
    public Exception? NextUseExistingFailure { get; set; }

    /// <summary>Every address passed to a clone, in order.</summary>
    public IReadOnlyList<SshAddress> Cloned
    {
        get { lock (gate) { return [.. cloned]; } }
    }

    /// <summary>Every address passed to a use of an existing clone, in order.</summary>
    public IReadOnlyList<SshAddress> UsedExisting
    {
        get { lock (gate) { return [.. reused]; } }
    }

    /// <summary>How many passes have started.</summary>
    public int PassesStarted => Volatile.Read(ref passesStarted);

    /// <summary>The most passes that ever ran at the same time.</summary>
    public int MaxConcurrentPasses => Volatile.Read(ref maxActivePasses);

    /// <summary>The device name of the most recent pass or clone connection.</summary>
    public string? LastDeviceName { get; private set; }

    /// <summary>The host-key decisions Sync gave to offered keys, in order.</summary>
    public List<HostKeyVerdict> Verdicts { get; } = [];

    /// <summary>Whether any pass or clone has used a connection.</summary>
    public int ConnectionsOpened => Volatile.Read(ref connectionsOpened);

    /// <summary>Makes a folder a clone of <paramref name="address"/>, as an earlier setup would have.</summary>
    public void SeedClone(string folder, SshAddress address)
    {
        lock (gate) { origins[folder] = address; }
    }

    /// <summary>Queues a held pass for the next unscripted pass slot.</summary>
    public PassScript ScriptPass()
    {
        var script = new PassScript();
        lock (gate) { scripts.Enqueue(script); }
        return script;
    }

    /// <summary>Completes once at least <paramref name="count"/> passes have started.</summary>
    public Task WaitForPassesStartedAsync(int count)
    {
        TaskCompletionSource reached;
        lock (gate)
        {
            if (passesStarted >= count)
                return Task.CompletedTask;
            reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            passWaiters.Add((count, reached));
        }

        return reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <inheritdoc />
    public GitReceipt? ReadReceipt(string folder)
    {
        lock (gate) { return receipts.GetValueOrDefault(folder); }
    }

    /// <inheritdoc />
    public void WriteReceipt(string folder, GitReceipt receipt)
    {
        lock (gate)
        {
            if (NextWriteReceiptFailure is { } failure)
            {
                NextWriteReceiptFailure = null;
                throw failure;
            }

            receipts[folder] = receipt;
        }
    }

    /// <summary>When set, the next receipt write fails with it, and the failure is then forgotten.</summary>
    public Exception? NextWriteReceiptFailure { get; set; }

    /// <summary>Runs when an existing clone is about to be used, so a test can record what was true at that moment.</summary>
    public Action? OnUseExisting { get; set; }

    /// <summary>How many times the origin of a clone was read.</summary>
    public int OriginReads => Volatile.Read(ref originReads);

    /// <summary>When set, reading a clone's origin throws it.</summary>
    public Exception? OriginReadFailure { get; set; }

    /// <inheritdoc />
    public string? ReadOrigin(string folder)
    {
        Interlocked.Increment(ref originReads);
        if (OriginReadFailure is { } failure)
            throw failure;
        lock (gate) { return origins.TryGetValue(folder, out var origin) ? origin.Text : null; }
    }

    /// <inheritdoc />
    public void UseExisting(string folder, SshAddress address)
    {
        OnUseExisting?.Invoke();
        lock (gate)
        {
            reused.Add(address);
            if (NextUseExistingFailure is { } failure)
            {
                NextUseExistingFailure = null;
                throw failure;
            }

            if (origins.TryGetValue(folder, out var origin) && origin != address)
                receipts.Remove(folder);
            origins[folder] = address;
        }
    }

    /// <inheritdoc />
    public Task Clone(GitConnection connection, SshAddress address, string folder, CancellationToken cancellationToken)
    {
        Exception? failure;
        lock (gate)
        {
            cloned.Add(address);
            failure = NextCloneFailure;
            NextCloneFailure = null;
        }

        Interlocked.Increment(ref connectionsOpened);
        LastDeviceName = connection.DeviceName;
        try
        {
            Offer(connection, address);
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }

        if (failure is not null)
            return Task.FromException(failure);
        lock (gate) { origins[folder] = address; }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<GitPassResult> RunPass(GitConnection connection, string folder, CancellationToken cancellationToken)
    {
        PassScript? script;
        SshAddress address;
        lock (gate)
        {
            script = scripts.Count > 0 ? scripts.Dequeue() : null;
            address = origins.TryGetValue(folder, out var found)
                ? found
                : throw new GitFailureException(SyncFailure.Repository, "The folder is not a seeded clone.");
        }

        Interlocked.Increment(ref connectionsOpened);
        LastDeviceName = connection.DeviceName;
        var active = Interlocked.Increment(ref activePasses);
        InterlockedMax(ref maxActivePasses, active);
        NotePassStarted();
        try
        {
            Offer(connection, address);
            if (script is null)
                return DefaultResult;
            if (script.Cleanup is { } cleanup)
                connection.Cleanup.Add(cleanup.Task);
            script.Started.TrySetResult();
            await using var registration = cancellationToken.Register(script.CancellationObserved.SetResult).ConfigureAwait(false);
            return await script.Finish.Task.ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref activePasses);
        }
    }

    private void Offer(GitConnection connection, SshAddress address)
    {
        if (OfferedHostKey is not { } key)
            return;
        var verdict = connection.VerifyHostKey(address.Host, address.Port, key.Algorithm, key.Fingerprint);
        Verdicts.Add(verdict);
        if (verdict == HostKeyVerdict.Unknown)
            throw new GitFailureException(SyncFailure.UnknownHostKey, "The host key is unknown.");
        if (verdict == HostKeyVerdict.Changed)
            throw new GitFailureException(SyncFailure.ChangedHostKey, "The host key changed.");
    }

    private void NotePassStarted()
    {
        List<TaskCompletionSource> ready = [];
        lock (gate)
        {
            passesStarted++;
            for (var i = passWaiters.Count - 1; i >= 0; i--)
            {
                if (passWaiters[i].Count <= passesStarted)
                {
                    ready.Add(passWaiters[i].Reached);
                    passWaiters.RemoveAt(i);
                }
            }
        }

        foreach (var reached in ready)
            reached.TrySetResult();
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }

    /// <summary>One held pass. It runs until the test completes, fails, or cancels it.</summary>
    public sealed class PassScript
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<GitPassResult> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource? Cleanup { get; private set; }

        /// <summary>Makes the pass add transport cleanup that stays pending until <see cref="FinishCleanup"/>, as a transport closing after EOF does.</summary>
        public void HoldTransportCleanup() => Cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Ends the held transport cleanup.</summary>
        public void FinishCleanup() => Cleanup!.TrySetResult();

        /// <summary>Completes once the held pass is running.</summary>
        public Task HasStarted => Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        /// <summary>Completes once Sync has asked the held pass to cancel.</summary>
        public Task WasCancelled => CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));

        /// <summary>Ends the held pass with a result.</summary>
        public void Complete(bool hasPendingChanges = false) => Finish.TrySetResult(new GitPassResult(hasPendingChanges));

        /// <summary>Ends the held pass with a failure.</summary>
        public void Fail(Exception failure) => Finish.TrySetException(failure);

        /// <summary>Ends the held pass as cancelled, after its cleanup.</summary>
        public void EndCancelled() => Finish.TrySetCanceled();
    }
}
