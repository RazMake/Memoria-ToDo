using System.IO.Pipes;

namespace Memoria.ToDo.Api.Client;

/// <summary>The client end of a real named pipe on this PC.</summary>
internal sealed class NamedPipeClient(string pipeName, PipeOptions options) : IPipeClientStream
{
    private readonly NamedPipeClientStream _pipe = new(".", pipeName, PipeDirection.InOut, options);

    public Stream Stream => _pipe;

    public Task ConnectAsync(int timeoutMilliseconds, CancellationToken cancellationToken) =>
        _pipe.ConnectAsync(timeoutMilliseconds, cancellationToken);

    public ValueTask DisposeAsync() => _pipe.DisposeAsync();

    /// <summary>The factory <see cref="MemoriaClient.Connect()"/> uses.</summary>
    public static IPipeClientStream Create(string pipeName, PipeOptions options) => new NamedPipeClient(pipeName, options);
}
