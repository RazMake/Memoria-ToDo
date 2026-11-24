using System.IO.Pipes;
using Grpc.Net.Client;
using Memoria.ToDo.Api.Contract;
namespace Memoria.ToDo.Api.Client;

/// <summary>
/// The local API of the app running for this Windows account. Connects on the first call.
/// A call fails with StatusCode.Unavailable when the app isn't running, another account owns the pipe,
/// or the elevation differs. Dispose closes the connection.
/// </summary>
public sealed class MemoriaClient : IDisposable
{
    private const int ConnectTimeoutMilliseconds = 1000;

    // The contract allows replies of up to 64 MB, while gRPC's default is 4 MB.
    private const int MaxReplyBytes = 64 * 1024 * 1024;

    // CurrentUserOnly refuses a pipe another account made; gRPC needs asynchronous use.
    private const PipeOptions Options = PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous;

    private readonly GrpcChannel _channel;

    private MemoriaClient(string pipeName, PipeClientFactory createPipe)
    {
        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            MaxReceiveMessageSize = MaxReplyBytes,
            DisposeHttpClient = true,
            HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = (_, cancellationToken) => ConnectAsync(createPipe, pipeName, cancellationToken),
            },
        });
        Tasks = new V1.Tasks.TasksClient(_channel);
        Contacts = new V1.Contacts.ContactsClient(_channel);
        Sync = new V1.Sync.SyncClient(_channel);
        Settings = new V1.Settings.SettingsClient(_channel);
    }

    /// <summary>Makes a client for this account's app. Opens no pipe until the first call.</summary>
    public static MemoriaClient Connect() => Connect(LocalApiPipe.ForCurrentUser());

    /// <summary>Connects to the pipe named <paramref name="pipeName"/>, as <see cref="Connect()"/> does to the current account's, on the first call.</summary>
    internal static MemoriaClient Connect(string pipeName) => new(pipeName, NamedPipeClient.Create);

    /// <summary>Connects through <paramref name="createPipe"/> on the first call.</summary>
    internal static MemoriaClient Connect(PipeClientFactory createPipe) => new(LocalApiPipe.ForCurrentUser(), createPipe);

    public V1.Tasks.TasksClient Tasks { get; }

    public V1.Contacts.ContactsClient Contacts { get; }

    public V1.Sync.SyncClient Sync { get; }

    public V1.Settings.SettingsClient Settings { get; }

    public void Dispose() => _channel.Dispose();

    private static async ValueTask<Stream> ConnectAsync(PipeClientFactory createPipe, string pipeName, CancellationToken cancellationToken)
    {
        var pipe = createPipe(pipeName, Options);
        try
        {
            await pipe.ConnectAsync(ConnectTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            return pipe.Stream;
        }
        catch (Exception ex)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            if (ex is OperationCanceledException)
            {
                throw;
            }

            // gRPC reports a failure with an IOException in its chain as UNAVAILABLE.
            throw new IOException($"Could not connect to the app's pipe '{pipeName}': {ex.Message}", ex);
        }
    }
}
