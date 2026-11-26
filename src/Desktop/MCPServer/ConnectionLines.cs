using System.Text.Json;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>
/// What each assistant needs to connect: the lines Configure shows to copy. Each one starts the app's executable with
/// <see cref="McpServer.BridgeArgument"/>, which bridges the assistant's standard input and output to the pipe.
/// </summary>
internal static class ConnectionLines
{
    private const string ServerName = "memoria-todo";

    private static readonly JsonSerializerOptions Quoting = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The lines for VS Code, GitHub Copilot CLI, Claude Desktop, Claude Code, and Codex, in that order.</summary>
    public static IReadOnlyList<McpConnection> For(string bridgePath)
    {
        var path = JsonSerializer.Serialize(bridgePath, Quoting); // a quoted string; JSON and TOML spell a path the same way
        return
        [
            new("VS Code", $$"""{ "servers": { "{{ServerName}}": { "type": "stdio", "command": {{path}}, "args": ["{{McpServer.BridgeArgument}}"] } } }"""),
            new("GitHub Copilot CLI", $"copilot mcp add {ServerName} -- {path} {McpServer.BridgeArgument}"),
            new("Claude Desktop", $$"""{ "mcpServers": { "{{ServerName}}": { "command": {{path}}, "args": ["{{McpServer.BridgeArgument}}"] } } }"""),
            new("Claude Code", $"claude mcp add {ServerName} -- {path} {McpServer.BridgeArgument}"),
            new("Codex", $$"""
                [mcp_servers.{{ServerName}}]
                command = {{path}}
                args = ["{{McpServer.BridgeArgument}}"]
                """),
        ];
    }
}
