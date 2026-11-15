using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// One View Tasks window's requests for sync passes. It asks the shared service for a pass and waits for it, knows that
/// one is running, and keeps why a pass it requested failed. It never stops the shared pass: disposing cancels only this
/// window's wait. The service raises its status on any thread, so changes are told through <c>post</c>.
/// </summary>
internal sealed class SyncRequests : IDisposable
{
    private readonly ISyncService _service;
    private readonly Action<Action> _post;
    private readonly Action _changed;
    private CancellationTokenSource? _wait;
    private bool _closed;

    public SyncRequests(ISyncService service, Action<Action> post, Action changed)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        _post = post;
        _changed = changed;
        _service.StatusChanged += OnStatusChanged;
    }

    /// <summary>Whether a pass this window requested, or any other, is running.</summary>
    public bool IsWorking => _wait is not null || _service.Status.State == SyncState.Working;

    /// <summary>Why the last pass this window requested failed, or empty.</summary>
    public string FailureText { get; private set; } = "";

    /// <summary>
    /// Requests a pass and waits for it, or does nothing while a pass this window requested is still running.
    /// A failure is kept in <see cref="FailureText"/>, never thrown, so callers may await or observe it freely.
    /// </summary>
    public async Task SyncAsync()
    {
        if (_closed || _wait is not null)
        {
            return;
        }

        using var wait = new CancellationTokenSource();
        _wait = wait;
        FailureText = "";
        _changed();
        try
        {
            var status = await _service.SyncNowAsync(wait.Token);
            if (!_closed && status.State is SyncState.Error or SyncState.Offline)
            {
                FailureText = status.Details ?? $"Sync failed: {status.Failure?.ToString() ?? status.State.ToString()}.";
            }
        }
        catch (OperationCanceledException) when (wait.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
            // The service stopped while the app shuts down; nothing is left to sync and nothing to report.
        }
        catch (Exception failure)
        {
            if (!_closed)
            {
                FailureText = failure.Message;
            }
        }
        finally
        {
            _wait = null;
            if (!_closed)
            {
                _changed();
            }
        }
    }

    public void Dispose()
    {
        _closed = true;
        _service.StatusChanged -= OnStatusChanged;
        _wait?.Cancel();
    }

    private void OnStatusChanged(object? sender, EventArgs e) =>
        _post(() =>
        {
            if (!_closed)
            {
                _changed();
            }
        });
}
