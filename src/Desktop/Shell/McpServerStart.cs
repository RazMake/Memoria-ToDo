using Memoria.ToDo.Desktop.McpServer;
using Memoria.ToDo.Shared.Sync;
using Serilog;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// Starts the MCP server once, on the UI thread, when Sync publishes that local initialization finished, whether it
/// succeeded or failed. Unlike <see cref="StartupNavigation"/>, a user's own navigation doesn't cancel it; only
/// <see cref="Cancel"/>, at shutdown, does.
/// </summary>
internal sealed class McpServerStart(
    ISyncSetup setup,
    IMcpServerLifetime server,
    Func<IUiThread> captureUiThread,
    ILogger logger)
{
    private readonly object gate = new();
    private IUiThread? ui;
    private bool cancelled;

    /// <summary>Starts listening on the UI thread, before Sync starts. Does nothing after <see cref="Cancel"/>.</summary>
    public void Begin()
    {
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

    /// <summary>Drops the pending start, for good.</summary>
    public void Cancel()
    {
        lock (gate)
        {
            cancelled = true;
        }

        setup.Changed -= OnChanged;
    }

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
            StartServer();
        }
        else
        {
            ui.Post(StartServer);
        }
    }

    // Runs on the UI thread; a shutdown since the publication still wins.
    private void StartServer()
    {
        lock (gate)
        {
            if (cancelled)
            {
                return;
            }

            cancelled = true;
        }

        logger.Information("Starting the MCP server after local initialization");
        server.Start();
    }
}
