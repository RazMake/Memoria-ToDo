using System.Threading.Channels;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// One end of an in-memory, two-way byte stream, such as a pipe: what one end writes the other reads, and an end that
/// is disposed or told it is done ends the other's reading. It stands in for the pipe, standard input, and standard
/// output in the bridge's unit tests, with no real I/O.
/// </summary>
internal sealed class MemoryDuplex : Stream
{
    private readonly Channel<byte[]> _incoming;
    private readonly Channel<byte[]> _outgoing;
    private byte[] _rest = [];
    private int _restAt;

    private MemoryDuplex(Channel<byte[]> incoming, Channel<byte[]> outgoing)
    {
        _incoming = incoming;
        _outgoing = outgoing;
    }

    /// <summary>The two ends: what <c>First</c> writes <c>Second</c> reads, and the other way round.</summary>
    public static (MemoryDuplex First, MemoryDuplex Second) Pair()
    {
        var toSecond = Channel.CreateUnbounded<byte[]>();
        var toFirst = Channel.CreateUnbounded<byte[]>();
        return (new MemoryDuplex(toFirst, toSecond), new MemoryDuplex(toSecond, toFirst));
    }

    /// <summary>Tells the other end that nothing more comes, as closing standard input does.</summary>
    public void CompleteWriting() => _outgoing.Writer.TryComplete();

    /// <summary>Writes text as UTF-8.</summary>
    public void WriteText(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        Write(bytes, 0, bytes.Length);
    }

    /// <summary>Reads until the other end is done, as UTF-8 text, failing after the unit-test wait bound.</summary>
    public async Task<string> ReadToEndAsync() => await new StreamReader(this, System.Text.Encoding.UTF8).ReadToEndAsync().WaitAsync(TestWait.ForSignal);

    /// <summary>Reads exactly <paramref name="count"/> bytes, failing after the unit-test wait bound.</summary>
    public async Task<byte[]> ReadExactlyAsync(int count)
    {
        var bytes = new byte[count];
        await ReadExactlyAsync(bytes.AsMemory()).AsTask().WaitAsync(TestWait.ForSignal);
        return bytes;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        while (_restAt == _rest.Length)
        {
            try
            {
                _rest = await _incoming.Reader.ReadAsync(cancellationToken);
                _restAt = 0;
            }
            catch (ChannelClosedException)
            {
                return 0;
            }
        }

        var count = Math.Min(buffer.Length, _rest.Length - _restAt);
        _rest.AsMemory(_restAt, count).CopyTo(buffer);
        _restAt += count;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_outgoing.Writer.TryWrite(buffer.ToArray()))
        {
            throw new IOException("The other end is closed.");
        }

        return ValueTask.CompletedTask;
    }

    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _outgoing.Writer.TryComplete();
        }

        base.Dispose(disposing);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
