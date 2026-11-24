using System.Threading.Channels;
using Grpc.Core;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>
/// The writer a stream handler sends its messages to, standing in for a client's connection. A test reads each message as it
/// arrives, and can make the reader slow: with <paramref name="holdWrites"/>, every write waits for <see cref="ReleaseWrite"/>, so
/// the handler stays inside a write while changes pile up. Waits only bound a failure.
/// </summary>
internal sealed class RecordingStreamWriter<T>(bool holdWrites = false) : IServerStreamWriter<T>
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly Channel<T> delivered = Channel.CreateUnbounded<T>();
    private readonly SemaphoreSlim entered = new(0);
    private readonly SemaphoreSlim permits = new(0);
    private int enteredCount;

    /// <inheritdoc />
    public WriteOptions? WriteOptions { get; set; }

    /// <summary>How many writes the handler has started, finished or held.</summary>
    public int Entered => Volatile.Read(ref enteredCount);

    /// <inheritdoc />
    public async Task WriteAsync(T message)
    {
        Interlocked.Increment(ref enteredCount);
        entered.Release();
        if (holdWrites)
            await permits.WaitAsync(Bound);
        delivered.Writer.TryWrite(message);
    }

    /// <summary>Completes when the handler starts its next write, one start per call.</summary>
    public Task EnteredAsync() => entered.WaitAsync(Bound);

    /// <summary>Lets one held write finish.</summary>
    public void ReleaseWrite() => permits.Release();

    /// <summary>The next message the handler wrote, waiting for it.</summary>
    public async Task<T> NextAsync()
    {
        using var bound = new CancellationTokenSource(Bound);
        return await delivered.Reader.ReadAsync(bound.Token);
    }
}
