namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>The MCP server as the composition root holds it: the part for Shell and the part for Configure.</summary>
public interface IMcpServer : IMcpServerLifetime, IMcpServerStatus
{
}
