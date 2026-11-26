using Memoria.ToDo.Shared.Sync;
using Serilog;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// Chooses the window an automatic start opens, once, when Sync publishes that local initialization finished,
/// whether it succeeded or failed: Configure when the device isn't configured; otherwise Task View unless the app
/// started at sign-in, then Configure when a shortcut didn't register. A manual navigation before that
/// publication, or shutdown, cancels it.
/// </summary>
internal sealed class StartupNavigation(
    ISyncSetup setup,
    Action showConfigure,
    Action showViewTasks,
    bool startedAtSignIn,
    Func<IUiThread> captureUiThread,
    ILogger logger)
{
    private readonly object gate = new();
    private IUiThread? ui;
    private bool cancelled;
    private bool stopped;
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

            ui = captureUiThread();
        }

        setup.Changed += OnChanged;
    }

    /// <summary>
    /// A shortcut failed to register after the start: it joins the pending automatic choice, so Configure opens last,
    /// or, when the choice already ran or was dropped, Configure opens at once.
    /// </summary>
    public void ShortcutFailed()
    {
        if (IsStopped)
        {
            logger.Information("Configure was not opened for a failed shortcut: the app is stopping");
            return;
        }

        lock (gate)
        {
            if (!cancelled)
            {
                shortcutFailed = true;
                return;
            }
        }

        showConfigure();
    }

    public void Stop()
    {
        lock (gate)
        {
            stopped = true;
        }
        Cancel();
    }

    public Action WhileRunning(Action action) => () =>
    {
        if (IsStopped)
        {
            logger.Information("A window action was ignored: the app is stopping");
            return;
        }
        action();
    };

    private bool IsStopped
    {
        get { lock (gate) { return stopped; } }
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
    public Action ByUser(Action show) => WhileRunning(() =>
    {
        Cancel();
        show();
    });

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
        if (ui!.IsCurrent)
        {
            Navigate();
        }
        else
        {
            ui.Post(Navigate);
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
