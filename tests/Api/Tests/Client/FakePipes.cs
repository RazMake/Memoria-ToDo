using System.IO.Pipes;
using Memoria.ToDo.Api.Client;

namespace Memoria.ToDo.Api.Client.Tests;

/// <summary>
/// Stands in for the pipe's client end: records how the client opens and connects it, and fails the connect the way
/// a missing server or another account's pipe does, so no pipe is needed.
/// </summary>
internal sealed class FakePipes
{
    /// <summary>What the connect throws; null connects to a pipe that carries nothing.</summary>
    public Exception? ConnectFailure { get; init; }

    public List<Opened> Pipes { get; } = [];

    public PipeClientFactory Factory => (name, options) =>
    {
        var pipe = new Opened(name, options, ConnectFailure);
        Pipes.Add(pipe);
        return pipe;
    };

    internal sealed class Opened(string pipeName, PipeOptions options, Exception? failure) : IPipeClientStream
    {
        public string PipeName { get; } = pipeName;

        public PipeOptions Options { get; } = options;

        public int? ConnectTimeoutMilliseconds { get; private set; }

        public bool Disposed { get; private set; }

        public Stream Stream => Stream.Null;

        public Task ConnectAsync(int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            ConnectTimeoutMilliseconds = timeoutMilliseconds;
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
