namespace Memoria.ToDo.Desktop.Updates;

/// <summary>One release's notes in Markdown; null when it was published without any.</summary>
public sealed record ReleaseNotes(string Version, string? Markdown);
