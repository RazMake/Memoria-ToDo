using System.IO.Pipes;

namespace Memoria.ToDo.Api.Client;

/// <summary>One client end of a named pipe: the seam that lets tests see how the client connects without a pipe.</summary>
internal interface IPipeClientStream : IAsyncDisposable
{
    /// <summary>Connects, or throws (TimeoutException when no server listens in time) after <paramref name="timeoutMilliseconds"/>.</summary>
    Task ConnectAsync(int timeoutMilliseconds, CancellationToken cancellationToken);

    /// <summary>The connected pipe.</summary>
    Stream Stream { get; }
}

/// <summary>Makes the client end for the named pipe on this PC, with the given options.</summary>
internal delegate IPipeClientStream PipeClientFactory(string pipeName, PipeOptions options);
