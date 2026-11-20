namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// What one read of the kept log found. <paramref name="UnreadableLines"/> counts broken lines that were skipped.
/// <paramref name="WriteProblem"/> is the writer's failure, or null while it writes.
/// </summary>
public sealed record LogRead(IReadOnlyList<LogEntry> Entries, int UnreadableLines, string? WriteProblem);
