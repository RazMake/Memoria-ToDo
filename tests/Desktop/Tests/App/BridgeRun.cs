using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The app as the bridge sees it: whether it runs, and a start that can make its pipe appear.</summary>
internal sealed class FakeBridgeApp : IBridgeApp
{
    private int _starts;

    /// <summary>What the one-instance signal says.</summary>
    public bool Running { get; set; }

    /// <summary>What a start does, such as making the pipe exist. Null does nothing.</summary>
    public Action? OnStart { get; set; }

    /// <summary>The failure a start throws, or null.</summary>
    public Exception? StartFails { get; set; }

    public int Starts => Volatile.Read(ref _starts);

    public bool IsRunning() => Running;

    public void Start()
    {
        Interlocked.Increment(ref _starts);
        if (StartFails is not null)
        {
            throw StartFails;
        }

        OnStart?.Invoke();
    }
}

/// <summary>
/// The pipe as the bridge sees it: absent until <see cref="Appear"/> gives it a stream, or failing with
/// <see cref="Fails"/>. Every attempt to connect is counted.
/// </summary>
internal sealed class FakeBridgePipe : IBridgePipe
{
    private int _attempts;
    private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Stream? _server;

    public int Attempts => Volatile.Read(ref _attempts);

    /// <summary>Completes when an attempt first got the pipe's stream.</summary>
    public Task Connected => _connected.Task;

    /// <summary>What a connect throws, such as the pipe of another user, or null.</summary>
    public Exception? Fails { get; set; }

    /// <summary>Makes the pipe exist. The bridge's end is the returned stream; the app's end is <paramref name="app"/>.</summary>
    public void Appear(out MemoryDuplex app)
    {
        var (bridgeEnd, appEnd) = MemoryDuplex.Pair();
        Volatile.Write(ref _server, bridgeEnd);
        app = appEnd;
    }

    public Task<Stream?> TryConnectAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attempts);
        if (Fails is not null)
        {
            return Task.FromException<Stream?>(Fails);
        }

        var server = Volatile.Read(ref _server);
        if (server is not null)
        {
            _connected.TrySetResult();
        }

        return Task.FromResult(server);
    }
}

/// <summary>One run of the bridge over in-memory input, output, and error, on a fake clock.</summary>
internal sealed class BridgeRun
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    private readonly StringWriter _error = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly MemoryDuplex _inputOfBridge;
    private readonly MemoryDuplex _outputOfBridge;
    private Task<int>? _run;

    public BridgeRun(FakeBridgeApp app, FakeBridgePipe pipe)
    {
        App = app;
        Pipe = pipe;
        var (assistantOut, bridgeIn) = MemoryDuplex.Pair();
        var (bridgeOut, assistantIn) = MemoryDuplex.Pair();
        _inputOfBridge = bridgeIn;
        _outputOfBridge = bridgeOut;
        AssistantWrites = assistantOut;
        AssistantReads = assistantIn;
    }

    public FakeBridgeApp App { get; }

    public FakeBridgePipe Pipe { get; }

    public FakeTimeProvider Time { get; } = new();

    /// <summary>The assistant's end of the bridge's standard input: what it writes the bridge reads.</summary>
    public MemoryDuplex AssistantWrites { get; }

    /// <summary>The assistant's end of the bridge's standard output.</summary>
    public MemoryDuplex AssistantReads { get; }

    /// <summary>What the bridge wrote to its standard error.</summary>
    public string Error => _error.ToString();

    public bool Ended => _run is { IsCompleted: true };

    public void Start() => _run = new McpBridge(App, Pipe, Time).RunAsync(_inputOfBridge, _outputOfBridge, _error, _cancel.Token);

    /// <summary>Waits for the bridge to end and returns its exit code, failing after the unit-test wait bound.</summary>
    public Task<int> ExitCodeAsync() => _run!.WaitAsync(TestWait.ForSignal);

    /// <summary>
    /// Moves the fake clock forward second by second until the bridge ends, and returns how far it moved. Fails when
    /// the bridge is still running after <paramref name="limit"/>.
    /// </summary>
    public async Task<TimeSpan> ElapseUntilEndedAsync(TimeSpan limit)
    {
        var moved = await ElapseUntilAsync(_run!, limit);
        return moved;
    }

    /// <summary>
    /// Moves the fake clock forward second by second until <paramref name="done"/> completes, and returns how far it
    /// moved. Fails when it's still not done after <paramref name="limit"/>.
    /// </summary>
    public async Task<TimeSpan> ElapseUntilAsync(Task done, TimeSpan limit)
    {
        var moved = TimeSpan.Zero;
        while (!done.IsCompleted)
        {
            if (moved >= limit)
            {
                // The last step may have just ended it, on another thread.
                await done.WaitAsync(TestWait.ForSignal).ContinueWith(_ => { }, TaskScheduler.Default);
                return done.IsCompleted ? moved : throw new Xunit.Sdk.XunitException($"The bridge was still waiting after {limit}.");
            }

            await Task.WhenAny(done, Time.WaitForActiveTimerAsync());
            if (done.IsCompleted)
            {
                break;
            }

            Time.Advance(Step);
            moved += Step;
        }

        return moved;
    }

    /// <summary>Moves the fake clock forward by <paramref name="by"/> in one-second steps.</summary>
    public async Task ElapseAsync(TimeSpan by)
    {
        for (var moved = TimeSpan.Zero; moved < by && !_run!.IsCompleted; moved += Step)
        {
            await Task.WhenAny(_run, Time.WaitForActiveTimerAsync());
            Time.Advance(Step);
        }
    }
}
