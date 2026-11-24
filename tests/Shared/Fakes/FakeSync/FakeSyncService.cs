using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Shared.Fakes;

public sealed class FakeSyncService : ISyncService
{
    public ISyncStatus Status { get; private set; } = new FakeSyncStatus();
    public FakeSyncSetup SetupControl { get; } = new();
    public ISyncSetup Setup => SetupControl;
    private EventHandler? statusChanged;
    public event EventHandler? StatusChanged
    {
        add { statusChanged += value; StatusSubscriberCount++; }
        remove { statusChanged -= value; StatusSubscriberCount--; }
    }
    public int StatusSubscriberCount { get; private set; }
    public int StartCount { get; private set; }
    public int SyncCount { get; private set; }
    public int StopCount { get; private set; }
    public int DisposeCount { get; private set; }
    public bool HoldStartupPublication { get; set; }
    public Func<CancellationToken, Task<ISyncStatus>>? SyncOperation { get; set; }

    /// <summary>When set, <see cref="SyncNowAsync"/> returns a task that fails with it, as Sync's does when it can't start a pass.</summary>
    public SyncNotReadyException? NotReady { get; set; }
    public Func<CancellationToken, Task>? StopOperation { get; set; }

    /// <summary>
    /// The task the last <see cref="SyncNowAsync"/> call returned. Awaiting it on the UI thread resumes after the caller's
    /// own continuation, because both are posted to the dispatcher in the order they were registered.
    /// </summary>
    public Task<ISyncStatus>? LastSync { get; private set; }

    public void Publish(ISyncStatus status)
    {
        Status = status;
        statusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        StartCount++;
        if (!HoldStartupPublication)
        {
            SetupControl.Publish(SetupControl.Snapshot);
        }
    }

    public Task<ISyncStatus> SyncNowAsync(CancellationToken cancellationToken)
    {
        SyncCount++;
        LastSync = (NotReady is { } notReady
            ? Task.FromException<ISyncStatus>(notReady)
            : SyncOperation?.Invoke(cancellationToken) ?? Task.FromResult(Status)).WaitAsync(cancellationToken);
        return LastSync;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return StopOperation?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
