namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>One assistant's name, such as "VS Code", and the text to copy for it.</summary>
public sealed record McpConnection(string Assistant, string Text);
