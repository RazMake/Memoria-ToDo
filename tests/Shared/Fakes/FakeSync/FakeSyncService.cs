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
    public Func<CancellationToken, Task>? StopOperation { get; set; }

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
        return (SyncOperation?.Invoke(cancellationToken) ?? Task.FromResult(Status)).WaitAsync(cancellationToken);
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
