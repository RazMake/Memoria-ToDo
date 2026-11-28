namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>
/// <paramref name="IsOn"/> is the switch. <paramref name="Listening"/> is true while the pipe takes connections.
/// <paramref name="Failure"/> says why the server can't listen, or why the switch can't be read or saved; null when nothing failed.
/// </summary>
public sealed record McpServerState(bool IsOn, bool Listening, string? Failure);
