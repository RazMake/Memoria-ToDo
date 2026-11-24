using System.IO;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>The MCP server: the switch, the pipe, and the state they make, over the connection server that does the serving.</summary>
internal sealed class McpServerService(
    OnOffSettingFile switchFile, ToolRunner runner, ConnectionServer connections, UiContext ui,
    string pipeName, IReadOnlyList<McpConnection> connectionLines, ILogger logger) : IMcpServer, IMcpConnections
{
    private readonly object _gate = new();
    private McpServerState _state = new(false, false, null);
    private bool _started;
    private bool _stopped;
    private int _generation;
    private int _pipesServed;
    private PipeListener? _listener;
    private Task _listenerStopped = Task.CompletedTask;
    private Task? _stop;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public McpServerState State
    {
        get { lock (_gate) { return _state; } }
    }

    /// <inheritdoc />
    public IReadOnlyList<McpConnection> Connections => connectionLines;

    /// <inheritdoc />
    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                throw new InvalidOperationException("The MCP server was already started.");
            }

            _started = true;
        }

        ui.Capture();
        var (on, failure) = switchFile.Read();
        logger.Information("MCP server starting, switched {Switch}", on ? "on" : "off");
        var changed = Publish(_ => new McpServerState(on, false, failure));
        if (on)
        {
            Listen();
        }

        if (changed)
        {
            RaiseChanged();
        }
    }

    /// <inheritdoc />
    public void SetOn(bool on)
    {
        switchFile.Write(on);
        logger.Information("MCP server switched {Switch}", on ? "on" : "off");
        if (on)
        {
            Publish(state => state with { IsOn = true, Failure = null });
            Listen();
        }
        else
        {
            _ = StopListening();
            connections.EndAll();
            Publish(_ => new McpServerState(false, false, null));
        }

        RaiseChanged();
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_gate)
        {
            return _stop ??= StopCoreAsync();
        }
    }

    /// <inheritdoc />
    public Task ServeAsync(Stream input, Stream output) => connections.ServeAsync(input, output);

    private async Task StopCoreAsync()
    {
        logger.Information("MCP server stopping");
        lock (_gate)
        {
            _stopped = true;
        }

        var calls = runner.StopAsync();
        var listenerStopped = StopListening();
        await calls.ConfigureAwait(false);
        await connections.CloseAsync().ConfigureAwait(false);
        await listenerStopped.ConfigureAwait(false);
        if (Publish(state => state with { Listening = false }))
        {
            RaiseChanged();
        }

        logger.Information("MCP server stopped");
    }

    // Starts listening unless it already does, or the server isn't started, or is stopped.
    private void Listen()
    {
        PipeListener listener;
        Task previous;
        var done = new TaskCompletionSource();
        lock (_gate)
        {
            if (!_started || _stopped || _listener is not null)
            {
                return;
            }

            var generation = ++_generation;
            var scope = connections.Generation;
            listener = new PipeListener(
                pipeName, pipe => ServePipeAsync(pipe, scope), () => Volatile.Read(ref _pipesServed) > 0, logger,
                () => OnListening(generation), reason => OnFailed(generation, reason));
            _listener = listener;
            previous = _listenerStopped;
            _listenerStopped = done.Task;
        }

        _ = listener.Start(previous).ContinueWith(_ => done.TrySetResult(), TaskScheduler.Default);
    }

    // Serves one pipe instance, counted until it ends, in the scope its listener started in, so ending that scope ends it even when
    // the listener handed it over late.
    private async Task ServePipeAsync(Stream pipe, CancellationToken scope)
    {
        Interlocked.Increment(ref _pipesServed);
        try
        {
            await connections.ServeAsync(pipe, pipe, scope).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _pipesServed);
        }
    }

    // Stops the listener, if any, and returns the task that completes when it has stopped.
    private Task StopListening()
    {
        lock (_gate)
        {
            _generation++;
            _listener?.Stop();
            _listener = null;
            return _listenerStopped;
        }
    }

    private void OnListening(int generation)
    {
        if (Publish(state => generation == _generation ? state with { Listening = true } : state))
        {
            RaiseChanged();
        }
    }

    private void OnFailed(int generation, string reason)
    {
        bool changed;
        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }

            _listener = null;
            changed = PublishLocked(state => state with { Listening = false, Failure = reason });
        }

        if (changed)
        {
            RaiseChanged();
        }
    }

    private bool Publish(Func<McpServerState, McpServerState> next)
    {
        lock (_gate)
        {
            return PublishLocked(next);
        }
    }

    private bool PublishLocked(Func<McpServerState, McpServerState> next)
    {
        var state = next(_state);
        if (state == _state)
        {
            return false;
        }

        _state = state;
        return true;
    }

    private void RaiseChanged() => ui.Post(() => Changed?.Invoke(this, EventArgs.Empty));
}
