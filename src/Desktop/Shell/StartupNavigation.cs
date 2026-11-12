using System.Windows.Threading;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// Chooses the window an automatic start opens, once, when Sync publishes that local initialization finished,
/// whether it succeeded or failed: Configure when the device isn't configured; otherwise View Tasks unless the app
/// started at sign-in, then Configure when a shortcut didn't register. A manual navigation before that
/// publication, or shutdown, cancels it.
/// </summary>
internal sealed class StartupNavigation(
    ISyncSetup setup,
    Action showConfigure,
    Action showViewTasks,
    bool startedAtSignIn)
{
    private readonly object gate = new();
    private Dispatcher? ui;
    private bool cancelled;
    private bool shortcutFailed;

    /// <summary>
    /// Starts listening on the UI thread, before Sync starts. Does nothing after <see cref="Cancel"/>.
    /// A failed shortcut registration adds Configure to the one startup choice.
    /// </summary>
    public void Begin(bool shortcutRegistrationFailed)
    {
        shortcutFailed = shortcutRegistrationFailed;
        lock (gate)
        {
            if (cancelled)
            {
                return;
            }

            ui = Dispatcher.CurrentDispatcher;
        }

        setup.Changed += OnChanged;
    }

    /// <summary>Drops the pending automatic window, for good.</summary>
    public void Cancel()
    {
        lock (gate)
        {
            cancelled = true;
        }

        setup.Changed -= OnChanged;
    }

    /// <summary>Wraps a user's own navigation: it drops the pending automatic window, then shows the window.</summary>
    public Action ByUser(Action show) => () =>
    {
        Cancel();
        show();
    };

    private void OnChanged(object? sender, EventArgs e)
    {
        lock (gate)
        {
            if (cancelled)
            {
                return;
            }
        }

        setup.Changed -= OnChanged;
        if (ui!.CheckAccess())
        {
            Navigate();
        }
        else
        {
            ui.BeginInvoke(Navigate);
        }
    }

    // Runs on the UI thread; a shutdown or manual navigation since the publication still wins.
    private void Navigate()
    {
        lock (gate)
        {
            if (cancelled)
            {
                return;
            }

            cancelled = true;
        }

        if (!setup.State.IsConfigured)
        {
            showConfigure();
        }
        else
        {
            if (!startedAtSignIn)
            {
                showViewTasks();
            }

            if (shortcutFailed)
            {
                showConfigure();
            }
        }
    }
}
