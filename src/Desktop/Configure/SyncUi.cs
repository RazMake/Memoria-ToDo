using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// What one open window's sync controls share: the sync service, the setup actions, one "something changed" notice on
/// the UI thread, and the cancellation of the window's own operations. Disposing it detaches the notices and cancels
/// the window's waits; it never stops the shared sync.
/// </summary>
internal sealed class SyncUi : IDisposable
{
    private readonly UiNotices _notices;
    private readonly CancellationTokenSource _cancel = new();
    private bool _disposed;

    public SyncUi(ISyncService sync, ISetupActions actions, UiNotices notices)
    {
        Sync = sync;
        Actions = actions;
        _notices = notices;
        sync.StatusChanged += OnChanged;
        sync.Setup.Changed += OnChanged;
    }

    public ISyncService Sync { get; }

    public ISetupActions Actions { get; }

    /// <summary>Raised on the UI thread when the setup or the sync status changed, and after an operation ends.</summary>
    public event Action? Changed;

    /// <summary>
    /// Runs an operation of this window. A failure goes to <paramref name="showError"/> instead of escaping, and
    /// nothing is shown after the window's cancellation or disposal. Always ends by raising <see cref="Changed"/>.
    /// </summary>
    public async Task RunAsync(Func<CancellationToken, Task> operation, Action<string> showError)
    {
        try
        {
            await operation(_cancel.Token);
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            if (!_disposed)
            {
                showError(error.Message);
            }
        }

        RaiseChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Sync.StatusChanged -= OnChanged;
        Sync.Setup.Changed -= OnChanged;
        _cancel.Cancel();
        _cancel.Dispose();
    }

    private void OnChanged(object? sender, EventArgs e) => _notices.Post(RaiseChanged);

    private void RaiseChanged()
    {
        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }
}
