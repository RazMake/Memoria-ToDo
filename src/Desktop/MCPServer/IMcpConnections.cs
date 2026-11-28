using System.IO;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>
/// The seam between where connections come from and the server: the pipe listener hands each connection's streams to it, and
/// the tests hand it in-process streams. It serves whatever it is given, whether or not the switch is on.
/// </summary>
internal interface IMcpConnections
{
    /// <summary>
    /// Serves one assistant over the streams until it closes its input or the server ends the connection, then disposes both
    /// streams. Never throws for a failed connection; it logs it.
    /// </summary>
    Task ServeAsync(Stream input, Stream output);
}
