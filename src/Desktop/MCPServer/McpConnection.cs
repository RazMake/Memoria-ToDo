namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>One assistant's name, such as "VS Code", and the text to copy for it.</summary>
public sealed record McpConnection(string Assistant, string Text);
