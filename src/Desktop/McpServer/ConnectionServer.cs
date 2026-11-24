using System.IO;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>
/// Serves assistants over streams, each connection with its own protocol session over the same tools. A connection ends when
/// the assistant closes its input, when the switch turns off, or when the server stops.
/// </summary>
internal sealed class ConnectionServer(IReadOnlyList<McpServerTool> tools, ILogger logger) : IMcpConnections
{
    private readonly ActiveWork _connections = new();
    private readonly CancellationTokenSource _closing = new();
    private CancellationTokenSource _generation = new();

    /// <summary>
    /// The scope of the connections served now: it is cancelled when <see cref="EndAll"/> ends them. A source of connections that
    /// serves later, such as a pipe listener, takes the scope at its start, so a connection it hands over late still ends with it.
    /// </summary>
    public CancellationToken Generation => Volatile.Read(ref _generation).Token;

    /// <inheritdoc />
    public Task ServeAsync(Stream input, Stream output) => ServeAsync(input, output, Generation);

    /// <summary>Serves like <see cref="ServeAsync(Stream, Stream)"/>, in the scope <paramref name="generation"/> that ends the connection.</summary>
    public async Task ServeAsync(Stream input, Stream output, CancellationToken generation)
    {
        if (!_connections.TryEnter())
        {
            logger.Debug("A connection was refused because the server is closing");
            await DisposeAsync(input, output).ConfigureAwait(false);
            return;
        }

        try
        {
            using var ending = CancellationTokenSource.CreateLinkedTokenSource(generation, _closing.Token);
            logger.Information("An assistant connected");
            try
            {
                var pending = new PendingRequests();
                var options = new McpServerOptions { ToolCollection = [.. tools] };
                pending.Watch(options);
                new ToolCallGate(ending.Token, logger).Watch(options);
                await using var session = ModelContextProtocol.Server.McpServer.Create(new StreamServerTransport(input, output), options);

                // Ending the session at once would lose the answers of calls still running or being cancelled.
                // Never disposed: it holds no timer, and a late Cancel after the session ended must stay harmless.
                var sessionEnd = new CancellationTokenSource();
                using var registration = ending.Token.UnsafeRegister(
                    static state =>
                    {
                        var (wait, end) = ((PendingRequests, CancellationTokenSource))state!;
                        _ = wait.WhenIdle().ContinueWith(_ => end.Cancel(), TaskScheduler.Default);
                    },
                    (pending, sessionEnd));
                await session.RunAsync(sessionEnd.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
            {
                logger.Debug(e, "A connection ended");
            }
            catch (Exception e)
            {
                logger.Error(e, "A connection failed: {Reason}", e.Message);
            }
            finally
            {
                await DisposeAsync(input, output).ConfigureAwait(false);
            }

            logger.Information("An assistant disconnected");
        }
        finally
        {
            _connections.Exit();
        }
    }

    /// <summary>Ends every connection of the scopes served so far, and starts a new scope. Later connections are served.</summary>
    public void EndAll() => Interlocked.Exchange(ref _generation, new CancellationTokenSource()).Cancel();

    /// <summary>Refuses new connections, ends the ones running, and completes when they have all ended.</summary>
    public Task CloseAsync()
    {
        var idle = _connections.CloseAsync();
        _closing.Cancel();
        return idle;
    }

    private static async Task DisposeAsync(Stream input, Stream output)
    {
        await input.DisposeAsync().ConfigureAwait(false);
        if (!ReferenceEquals(input, output))
        {
            await output.DisposeAsync().ConfigureAwait(false);
        }
    }
}
