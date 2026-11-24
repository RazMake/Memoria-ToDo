using System.IO;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The app's MCP pipe as the bridge sees it.</summary>
internal interface IBridgePipe
{
    /// <summary>One attempt to connect, never waiting for the pipe: its stream, or null when it doesn't exist yet. Throws when it can't be used, such as another user's pipe.</summary>
    Task<Stream?> TryConnectAsync(CancellationToken cancellationToken);
}
