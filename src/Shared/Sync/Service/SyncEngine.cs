using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>The sync service: joins concurrent requests into one pass, runs it, and publishes the status.</summary>
internal sealed class SyncEngine : ISyncService
{
    private readonly object gate = new();
    private readonly ITaskSynchronization tasks;
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
        ITaskSynchronization tasks,
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
        IRepositorySelection repositories)
    {
        this.repositories = repositories;
        this.boundary = boundary;
        this.tasks = tasks;
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
        tasks.LocalSaved += OnLocalSaved;
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

    // Stopping ends operations with these. A cancellation nobody requested, or any other disposed dependency, is an ordinary failure.
    private bool IsStopping(Exception ex, CancellationToken operation) =>
        (ex is OperationCanceledException && operation.IsCancellationRequested) || (ex is ObjectDisposedException && operations.IsStopped);

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
                catch (Exception ex) when (!IsStopping(ex, token))
                {
                    var (kind, details) = FailureReport.OfLocal(ex);
                    status.Update(s => s.Failed(kind, details, s.SkippedFiles));
                    setupModel.PublishFailed();
                }

                return 0;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStopping(ex, default))
        {
            // Stopping: nothing to publish.
        }
    }

    public Task<ISyncStatus> SyncNowAsync(CancellationToken cancellationToken) =>
        StartOrJoin().WaitAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        tasks.LocalSaved -= OnLocalSaved;
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

    private void RequestPass() => _ = RunScheduledAsync();

    private async Task RunScheduledAsync()
    {
        try
        {
            await StartOrJoin(false).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException || IsStopping(ex, default))
        {
            // Nothing to sync yet, or stopping: a scheduled pass just doesn't happen.
        }
    }

    // A user sync joins the newest pass that hasn't finished. A scheduled request during a pass queues one follow-up.
    private Task<ISyncStatus> StartOrJoin(bool user = true)
    {
        lock (gate)
        {
            if (operations.IsStopped)
                return Task.FromException<ISyncStatus>(new ObjectDisposedException(nameof(SyncEngine)));
            if (runningPass is null)
                return runningPass = Task.Run(TrackedPassAsync);
            return user ? queuedPass ?? runningPass : queuedPass ??= Task.Run(TrackedPassAsync);
        }
    }

    private async Task<ISyncStatus> TrackedPassAsync()
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
            return await operations.RunAsync(token => PassAsync(token, Release), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Release();
        }
    }

    private async Task<ISyncStatus> PassAsync(CancellationToken token, Action release)
    {
        string folder;
        try
        {
            folder = tasks.RepositoryFolder ?? throw new InvalidOperationException("No repository is selected.");
        }
        catch (ArgumentException ex)
        {
            Fail(release, SyncFailure.LocalStorage, "The saved repository folder is not usable: " + FailureReport.Describe(ex));
            return status.Current;
        }

        if (await RejectDeviceFolderAsync(folder, release).ConfigureAwait(false) is { } rejected)
            return rejected;

        try
        {
            if (!await worker.Run(() => keys.HasKey).ConfigureAwait(false))
                throw new InvalidOperationException("The device has no key yet.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail(release, SyncFailure.LocalStorage, FailureReport.Describe(ex));
            return status.Current;
        }

        var before = status.Current;
        scheduler.PassStarting();
        status.Update(s => s.Working());
        Func<StatusSnapshot, StatusSnapshot> outcome;
        var clean = false;
        try
        {
            // The linked token can lag behind a stop by a callback hop, so the stop flag is checked too.
            if (operations.IsStopped)
                throw new OperationCanceledException(token);
            token.ThrowIfCancellationRequested();
            (outcome, clean) = await RunPassAsync(folder, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || operations.IsStopped)
        {
            status.Update(_ => before);
            throw;
        }
        catch (Exception ex) when (!IsStopping(ex, token))
        {
            var (kind, details) = FailureReport.Of(ex);
            outcome = s => s.Failed(kind, details, []);
        }

        // The refresh outcome is folded in first, so the pass publishes exactly one final state.
        var refreshProblem = await TryRefreshAsync(token).ConfigureAwait(false);
        if (refreshProblem is not null)
        {
            var passOutcome = outcome;
            outcome = s =>
            {
                var finished = passOutcome(s);
                return finished.Failure is { } kind
                    ? finished.Failed(kind, $"{finished.Details} The setup could not be refreshed: {refreshProblem}", finished.SkippedFiles)
                    : finished.Failed(SyncFailure.Unexpected, refreshProblem, finished.SkippedFiles);
            };
            clean = false;
        }

        status.Update(outcome);
        if (refreshProblem is not null)
            setupModel.PublishFailed();
        release();
        scheduler.PassFinished(clean);
        return status.Current;
    }

    // The check can fail when the real location is unreadable; that is a returned status and no pass starts.
    private async Task<ISyncStatus?> RejectDeviceFolderAsync(string folder, Action release)
    {
        bool contains;
        try
        {
            contains = await worker.Run(() => boundary.Contains(folder)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail(release, SyncFailure.Repository, "The selected repository folder can't be checked against the device folder: " + ex.Message);
            return status.Current;
        }
        catch (ArgumentException ex)
        {
            // The saved folder, which came from the repository selection, is malformed.
            Fail(release, SyncFailure.LocalStorage, "The saved repository folder is not usable: " + FailureReport.Describe(ex));
            return status.Current;
        }

        if (!contains)
            return null;
        const string problem = "The selected repository folder contains the device folder.";
        status.Update(s => s.Failed(SyncFailure.Repository, problem, s.SkippedFiles));
        throw new InvalidOperationException(problem);
    }

    private async Task<(Func<StatusSnapshot, StatusSnapshot> Outcome, bool Clean)> RunPassAsync(string folder, CancellationToken token)
    {
        var (result, fingerprint) = await session.RunAsync(async connection =>
        {
            var pass = await git.RunPass(connection, folder, token).ConfigureAwait(false);
            return (pass, DeviceMetadata.Fingerprint(connection.Key.PublicKeyBlob.Span));
        }).ConfigureAwait(false);

        var now = time.GetUtcNow();
        await worker.Run(() => git.WriteReceipt(folder, new GitReceipt(now, fingerprint))).ConfigureAwait(false);
        var skipped = MergedSkippedFile.Combine(result.Skipped, tasks.SkippedFiles);
        if (skipped.Count > 0)
        {
            return (s => s.Succeeded(now, result.HasPendingChanges)
                .Failed(SyncFailure.InvalidFile, $"{skipped.Count} file(s) in the repository can't be read.", skipped), false);
        }

        return (s => s.Succeeded(now, result.HasPendingChanges), true);
    }

    // Returns what went wrong, or null. A stop propagates; any other failure is data for the final status.
    private async Task<string?> TryRefreshAsync(CancellationToken token)
    {
        try
        {
            await setupModel.RefreshAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (!IsStopping(ex, token))
        {
            return FailureReport.Describe(ex);
        }
    }

    private void Fail(Action release, SyncFailure kind, string details)
    {
        status.Update(s => s.Failed(kind, details, s.SkippedFiles));
        release();
        scheduler.PassFinished(false);
    }
}