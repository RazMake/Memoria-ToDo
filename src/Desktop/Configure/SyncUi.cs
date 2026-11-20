using Memoria.ToDo.Shared.Sync;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// What one open window's sync controls share: the sync service, the setup actions, one "something changed" notice on
/// the UI thread, and the cancellation of the window's own operations. Disposing it detaches the notices and cancels
/// the window's waits; it never stops the shared sync.
/// </summary>
internal sealed class SyncUi : IDisposable
{
    private readonly Action<Action> _post;
    private readonly CancellationTokenSource _cancel = new();
    private bool _disposed;

    /// <param name="post">Runs an action on the UI thread; it may be called from any thread.</param>
    /// <param name="logger">Where the window's failures are logged.</param>
    public SyncUi(ISyncService sync, ISetupActions actions, Action<Action> post, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Sync = sync;
        Actions = actions;
        _post = post;
        Logger = logger;
        sync.StatusChanged += OnChanged;
        sync.Setup.Changed += OnChanged;
    }

    public ISyncService Sync { get; }

    public ISetupActions Actions { get; }

    /// <summary>Where the sync controls of this window log the failures they catch and show.</summary>
    public ILogger Logger { get; }

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
            Logger.Error(error, "A sync setting operation failed");
            if (!_disposed)
            {
                showError(error.Message);
            }
        }

        RaiseChanged();
    }

    /// <summary>
    /// Runs one sync pass and passes the details of a failed pass (Error or Offline) to <paramref name="showFailure"/>.
    /// Meant to run inside <see cref="RunAsync"/>, which shows a thrown failure.
    /// </summary>
    public async Task SyncNowAsync(Action<string?> showFailure, CancellationToken token)
    {
        var status = await Sync.SyncNowAsync(token);
        if (SyncText.IsFailure(status))
        {
            showFailure(status.Details);
        }
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

    private void OnChanged(object? sender, EventArgs e) => _post(RaiseChanged);

    private void RaiseChanged()
    {
        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }
}
