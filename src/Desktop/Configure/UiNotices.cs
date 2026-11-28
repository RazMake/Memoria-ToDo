using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Brings notices that services raise on any thread to the UI thread. After <see cref="Stop"/>, no queued notice runs.
/// Create it on the UI thread.
/// </summary>
internal sealed class UiNotices
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private volatile bool _stopped;

    /// <summary>Runs the action now when called on the UI thread, otherwise queues it there. Does nothing once stopped.</summary>
    public void Post(Action action)
    {
        if (_stopped)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (!_stopped)
            {
                action();
            }
        });
    }

    /// <summary>Drops the notices still queued and any posted later.</summary>
    public void Stop() => _stopped = true;
}
