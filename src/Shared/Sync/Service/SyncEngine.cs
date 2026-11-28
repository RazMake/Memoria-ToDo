using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>The sync service: joins concurrent requests into one pass, runs it, and publishes the status.</summary>
internal sealed class SyncEngine : ISyncService
{
    private readonly object gate = new();
    private readonly ILogger logger;
    private readonly object statusLogGate = new();
    private (SyncState State, SyncFailure? Failure) lastLoggedStatus = (StatusSnapshot.Initial.State, StatusSnapshot.Initial.Failure);
    private readonly ISyncedRepository repository;
    private readonly IGitOperations git;
    private readonly IDeviceKeyStore keys;
    private readonly TimeProvider time;
    private readonly OwnedWorker worker;
    private readonly Operations operations;
    private readonly GitSession session;
    private readonly SetupModel setupModel;
    private readonly Published<StatusSnapshot> status;
    private readonly Scheduler scheduler;
    private readonly DeviceFolderBoundary boundary;
    private readonly IRepositorySelection repositories;
    private Task<ISyncStatus>? runningPass;
    private Task<ISyncStatus>? queuedPass;
    private Task? initialization;

    public SyncEngine(
        ISyncedRepository repository,
        IGitOperations git,
        IDeviceKeyStore keys,
        TimeProvider time,
        TimeSpan? periodicInterval,
        OwnedWorker worker,
        Operations operations,
        GitSession session,
        SetupModel setupModel,
        Published<StatusSnapshot> status,
        ISyncSetup setup,
        DeviceFolderBoundary boundary,
        IRepositorySelection repositories,
        ILogger logger)
    {
        this.logger = logger;
        this.repositories = repositories;
        this.boundary = boundary;
        this.repository = repository;
        this.git = git;
        this.keys = keys;
        this.time = time;
        this.worker = worker;
        this.operations = operations;
        this.session = session;
        this.setupModel = setupModel;
        this.status = status;
        Setup = setup;
        scheduler = new Scheduler(time, periodicInterval, RequestPass);
        status.Changed += OnStatusChanged;
        repository.LocalSaved += OnLocalSaved;
    }

    // Each change of the state or failure is logged once, however many updates published it.
    private void OnStatusChanged(object? sender, EventArgs e)
    {
        var current = status.Current;
        lock (statusLogGate)
        {
            if (lastLoggedStatus == (current.State, current.Failure))
                return;
            lastLoggedStatus = (current.State, current.Failure);
            logger.Information("Sync status is now {State}", current.State);
        }
    }

    public ISyncStatus Status => status.Current;

    public ISyncSetup Setup { get; }

    public event EventHandler? StatusChanged
    {
        add => status.Changed += value;
        remove => status.Changed -= value;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(operations.IsStopped, this);
        lock (gate)
        {
            initialization ??= Task.Run(StartAsync);
        }
    }

    private async Task StartAsync()
    {
        await InitializeAsync().ConfigureAwait(false);
        scheduler.Start();
    }

    // Once stopping has begun, whatever kind of failure ends an operation is part of the stop. Before that, a disposed dependency
    // or a cancellation nobody requested is an ordinary failure.
    private bool IsStopping(CancellationToken operation) => operations.IsStopped || operation.IsCancellationRequested;

    // Loads and publishes the setup, even without a key or clone, before any network pass. A failure is published, not thrown,
    // inside the operation so that Stop, which waits for the operation, also waits for the publication.
    private async Task InitializeAsync()
    {
        try
        {
            await operations.RunAsync(async token =>
            {
                try
                {
                    await worker.Run(repositories.Load).ConfigureAwait(false);
                    await setupModel.RefreshAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (!IsStopping(token))
                {
                    var (kind, details) = FailureReport.OfLocal(ex);
                    logger.LogFailure(kind, details, ex);
                    status.Update(s => s.Failed(kind, details, s.SkippedFiles));
                    setupModel.PublishFailed();
                }

                return 0;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception) when (IsStopping(default))
        {
            // Stopping: nothing to publish.
        }
    }

    public Task<ISyncStatus> SyncNowAsync(CancellationToken cancellationToken) =>
        StartOrJoin(SyncTrigger.Manual).WaitAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        repository.LocalSaved -= OnLocalSaved;
        scheduler.Dispose();
        await operations.StopAsync(cancellationToken).ConfigureAwait(false);
        Task? starting;
        lock (gate)
        {
            starting = initialization;
        }

        if (starting is not null)
            await starting.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));

    private void OnLocalSaved(object? sender, EventArgs e) => scheduler.LocalSaved();

    private void RequestPass(SyncTrigger trigger) => _ = RunScheduledAsync(trigger);

    private async Task RunScheduledAsync(SyncTrigger trigger)
    {
        try
        {
            await StartOrJoin(trigger).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException and not ObjectDisposedException || IsStopping(default))
        {
            // Nothing to sync yet, or stopping: a scheduled pass just doesn't happen. A disposed dependency is a real failure.
        }
    }

    // A user sync joins the newest pass that hasn't finished. A scheduled request during a pass queues one follow-up.
    private Task<ISyncStatus> StartOrJoin(SyncTrigger trigger)
    {
        var user = trigger == SyncTrigger.Manual;
        lock (gate)
        {
            if (operations.IsStopped)
                return Task.FromException<ISyncStatus>(new ObjectDisposedException(nameof(SyncEngine)));
            if (runningPass is null)
                return runningPass = Task.Run(() => TrackedPassAsync(trigger));
            return user ? queuedPass ?? runningPass : queuedPass ??= Task.Run(() => TrackedPassAsync(trigger));
        }
    }

    private async Task<ISyncStatus> TrackedPassAsync(SyncTrigger trigger)
    {
        var released = 0;
        void Release()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                lock (gate)
                {
                    runningPass = queuedPass;
                    queuedPass = null;
                }
            }
        }

        try
        {
            return await operations.RunAsync(token => PassAsync(token, Release, trigger), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Release();
        }
    }

    // The one restore point: whatever ends the pass once stopping began, including its setup refresh, is part of the
    // stop. The status from before the pass comes back, and Operations ends the caller as a stopped service.
    private async Task<ISyncStatus> PassAsync(CancellationToken token, Action release, SyncTrigger trigger)
    {
        var before = status.Current;
        try
        {
            var result = await RunOnePassAsync(token, release, trigger).ConfigureAwait(false);
            // Stopping can begin between a stop check and publishing a failure; the failure is still part of the stop.
            if (result.Failure is not null && IsStopping(token))
                throw new OperationCanceledException(token);
            return result;
        }
        catch (Exception ex) when (IsStopping(token))
        {
            status.Update(_ => before);
            if (ex is OperationCanceledException)
                throw;
            throw new OperationCanceledException(token);
        }
    }

    private async Task<ISyncStatus> RunOnePassAsync(CancellationToken token, Action release, SyncTrigger trigger)
    {
        string folder;
        try
        {
            folder = repository.Folder ?? throw new SyncNotReadyException("No repository is selected.");
        }
        catch (ArgumentException ex) when (!IsStopping(token))
        {
            Fail(release, SyncFailure.LocalStorage, "The saved repository folder is not usable: " + FailureReport.Describe(ex), ex);
            return status.Current;
        }

        if (await RejectDeviceFolderAsync(folder, release, token, trigger).ConfigureAwait(false) is { } rejected)
            return rejected;

        bool hasKey;
        try
        {
            hasKey = await worker.Run(() => keys.HasKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (!IsStopping(token))
        {
            var (kind, details) = FailureReport.OfLocal(ex);
            Fail(release, kind, details, ex);
            return status.Current;
        }

        if (!hasKey)
            throw new SyncNotReadyException("The device has no key yet.");

        scheduler.PassStarting();
        logger.Information("Sync pass started by {Trigger}", trigger);
        var startedAt = time.GetTimestamp();
        status.Update(s => s.Working());
        Func<StatusSnapshot, StatusSnapshot> outcome;
        var clean = false;
        try
        {
            // The linked token can lag behind a stop by a callback hop, so the stop flag is checked too.
            if (operations.IsStopped)
                throw new OperationCanceledException(token);
            token.ThrowIfCancellationRequested();
            (outcome, clean) = await RunPassAsync(folder, startedAt, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!IsStopping(token))
        {
            var failure = ex is PassFailedException { InnerException: { } inner } ? inner : ex;
            var (kind, details) = FailureReport.OfLocal(failure);
            logger.LogFailure(kind, details, failure);
            logger.Information("Sync pass ended after a failure in {DurationMs} ms", time.GetElapsedTime(startedAt).TotalMilliseconds);
            var pending = (ex as PassFailedException)?.HasPendingChanges;
            outcome = s => s.Failed(kind, details, []) with { HasPendingChanges = pending ?? s.HasPendingChanges };
        }

        // The refresh outcome is folded in first, so the pass publishes exactly one final state.
        var refreshProblem = await TryRefreshAsync(token).ConfigureAwait(false);
        if (refreshProblem is { } refresh)
        {
            var passOutcome = outcome;
            outcome = s =>
            {
                var finished = passOutcome(s);
                return finished.Failure is { } kind
                    ? finished.Failed(kind, $"{finished.Details} The setup could not be refreshed: {refresh.Details}", finished.SkippedFiles)
                    : finished.Failed(refresh.Kind, refresh.Details, finished.SkippedFiles);
            };
            clean = false;
        }

        // A failed pass that ends once stopping has begun is part of the stop, so it publishes nothing.
        if (IsStopping(token) && outcome(status.Current).Failure is not null)
            throw new OperationCanceledException(token);
        status.Update(outcome);
        if (refreshProblem is not null)
            setupModel.PublishFailed();
        release();
        scheduler.PassFinished(clean);
        return status.Current;
    }

    // The check can fail when the real location is unreadable; that is a returned status and no pass starts. A selected
    // folder that contains the device folder is thrown on to a manual sync, whose caller shows it; a scheduled pass
    // swallows it, so only that one logs it.
    private async Task<ISyncStatus?> RejectDeviceFolderAsync(string folder, Action release, CancellationToken token, SyncTrigger trigger)
    {
        bool contains;
        try
        {
            contains = await worker.Run(() => boundary.Contains(folder)).ConfigureAwait(false);
        }
        catch (ArgumentException ex) when (!IsStopping(token))
        {
            // The saved folder, which came from the repository selection, is malformed.
            Fail(release, SyncFailure.LocalStorage, "The saved repository folder is not usable: " + FailureReport.Describe(ex), ex);
            return status.Current;
        }
        catch (Exception ex) when (!IsStopping(token))
        {
            var (kind, details) = FailureReport.OfLocal(ex);
            Fail(release, kind, details, ex);
            return status.Current;
        }

        if (!contains)
            return null;
        if (IsStopping(token))
            throw new OperationCanceledException(token);
        const string problem = "The selected repository folder contains the device folder.";
        var failure = new SyncNotReadyException(problem);
        if (trigger != SyncTrigger.Manual)
            logger.LogFailure(SyncFailure.Repository, problem, failure);
        status.Update(s => s.Failed(SyncFailure.Repository, problem, s.SkippedFiles));
        throw failure;
    }

    private async Task<(Func<StatusSnapshot, StatusSnapshot> Outcome, bool Clean)> RunPassAsync(string folder, long startedAt, CancellationToken token)
    {
        // Once the Git work has ended knowing the pending changes, a later failure (closing the session, writing the
        // receipt) still shows what it found.
        bool? found = null;
        GitPassResult result;
        DateTimeOffset now;
        try
        {
            string fingerprint;
            (result, fingerprint) = await session.RunAsync(async connection =>
            {
                GitPassResult pass;
                try
                {
                    pass = await git.RunPass(connection, folder, token).ConfigureAwait(false);
                }
                catch (PassFailedException failed)
                {
                    found = failed.HasPendingChanges;
                    throw;
                }

                found = pass.HasPendingChanges;
                return (pass, DeviceMetadata.Fingerprint(connection.Key.PublicKeyBlob.Span));
            }).ConfigureAwait(false);

            now = time.GetUtcNow();
            await worker.Run(() => git.WriteReceipt(folder, new GitReceipt(now, fingerprint))).ConfigureAwait(false);
        }
        catch (Exception ex) when (found is { } pending && ex is not PassFailedException && !IsStopping(token))
        {
            throw new PassFailedException(ex, pending);
        }

        var skipped = MergedSkippedFile.Combine(merge: result.Skipped, reload: repository.SkippedFiles);
        foreach (var file in result.Skipped)
        {
            logger.Warning(
                "The merge skipped {Path}: {Reason} (needs a newer app: {RequiresUpdate})",
                file.RelativePath, file.Reason, file.RequiresUpdate);
        }

        logger.Information(
            "Sync pass ended: fetched {Fetched}, merged {Merged}, pushed {Pushed}, merge skipped {Skipped} file(s) in {DurationMs} ms",
            result.Fetched, result.Merged, result.Pushed, result.Skipped.Count, time.GetElapsedTime(startedAt).TotalMilliseconds);
        if (skipped.Count > 0)
        {
            var details = $"{skipped.Count} file(s) in the repository can't be read.";
            logger.LogFailure(SyncFailure.InvalidFile, details, null);
            return (s => s.Succeeded(now, result.HasPendingChanges)
                .Failed(SyncFailure.InvalidFile, details, skipped), false);
        }

        return (s => s.Succeeded(now, result.HasPendingChanges), true);
    }

    // Returns what went wrong, or null. A stop propagates; any other failure is data for the final status.
    private async Task<(SyncFailure Kind, string Details)?> TryRefreshAsync(CancellationToken token)
    {
        try
        {
            await setupModel.RefreshAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (!IsStopping(token))
        {
            var (kind, details) = FailureReport.OfLocal(ex);
            logger.LogFailure(kind, "The setup could not be refreshed: " + details, ex);
            return (kind, details);
        }
    }

    private void Fail(Action release, SyncFailure kind, string details, Exception failure)
    {
        logger.LogFailure(kind, details, failure);
        status.Update(s => s.Failed(kind, details, s.SkippedFiles));
        release();
        scheduler.PassFinished(false);
    }
}