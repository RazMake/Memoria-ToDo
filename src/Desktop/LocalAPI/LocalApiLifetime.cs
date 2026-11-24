using Serilog;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// Starts and stops the <see cref="IApiHost"/>, logs both, and closes the <see cref="ICallGate"/> when a stop begins.
/// </summary>
internal sealed class LocalApiLifetime : ILocalApiLifetime, ICallGate
{
    /// <summary>How long a stop waits for running calls, as the design says.</summary>
    public static readonly TimeSpan DefaultStopWait = TimeSpan.FromSeconds(5);

    private readonly IApiHost host;
    private readonly string pipeName;
    private readonly ILogger logger;
    private readonly TimeSpan stopWait;
    private readonly Lock gate = new();
    private Task<bool>? starting;
    private Task? stopping;
    private readonly CancellationTokenSource stopSignal = new();
    private volatile bool stopRequested;

    /// <summary><paramref name="stopWait"/> is how long a stop waits for running calls before it cancels the host's stop.</summary>
    public LocalApiLifetime(IApiHost host, string pipeName, ILogger logger, TimeSpan stopWait)
    {
        this.host = host;
        this.pipeName = pipeName;
        this.logger = logger;
        this.stopWait = stopWait;
    }

    /// <inheritdoc />
    public bool AcceptsCalls => !stopRequested;

    /// <inheritdoc />
    public CancellationToken Stopping => stopSignal.Token;

    /// <inheritdoc />
    public void Start()
    {
        lock (gate)
        {
            if (starting is not null || stopRequested)
                return;

            // Off the caller's thread, so a UI thread never waits for the bind.
            starting = Task.Run(ListenAsync);
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (gate)
        {
            if (stopping is null)
            {
                stopRequested = true;
                stopSignal.Cancel();
                stopping = StopHostAsync(starting);
            }

            return stopping;
        }
    }

    /// <summary>Starts the host; true when it listens. A failure is logged, never thrown.</summary>
    private async Task<bool> ListenAsync()
    {
        try
        {
            await host.StartAsync();
            logger.Information("The Local API listens on the pipe {PipeName}", pipeName);
            return true;
        }
        catch (Exception failure)
        {
            logger.Error(failure, "The Local API couldn't listen on the pipe {PipeName}, so the app runs without it", pipeName);
            return false;
        }
    }

    private async Task StopHostAsync(Task<bool>? start)
    {
        if (start is null || !await start)
            return;

        using var giveUp = new CancellationTokenSource(stopWait);
        try
        {
            await host.StopAsync(giveUp.Token);
            logger.Information("Stopped the Local API on the pipe {PipeName}", pipeName);
        }
        catch (OperationCanceledException) when (giveUp.IsCancellationRequested)
        {
            logger.Warning("The Local API on the pipe {PipeName} stopped after giving up on calls still running after {WaitSeconds} s", pipeName, stopWait.TotalSeconds);
        }
        catch (Exception failure)
        {
            logger.Error(failure, "The Local API on the pipe {PipeName} failed to stop cleanly", pipeName);
        }
    }
}
