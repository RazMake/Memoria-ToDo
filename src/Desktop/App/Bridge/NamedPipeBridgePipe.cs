using System.IO;
using System.IO.Pipes;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The real pipe client. It connects only to a pipe the same user made, so another user's program can't pose as the app.</summary>
internal sealed class NamedPipeBridgePipe(string pipeName) : IBridgePipe
{
    public async Task<Stream?> TryConnectAsync(CancellationToken cancellationToken)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync(0, cancellationToken);
            return client;
        }
        catch (TimeoutException)
        {
            // No pipe with this name yet, or the app's one instance is busy: the bridge tries again.
            await client.DisposeAsync();
            return null;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }
}
