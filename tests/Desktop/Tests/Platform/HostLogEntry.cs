using Serilog.Events;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// One log entry the platform's test program printed: its level, its rendered message, and its exception's text, empty
/// when there is none. The program's ConsoleLogSink prints the line this reads.
/// </summary>
internal sealed record HostLogEntry(LogEventLevel Level, string Message, string Exception)
{
    public const string LinePrefix = "log-entry ";
    private const char Separator = '\u001f';

    public static HostLogEntry Parse(string line)
    {
        var parts = line[LinePrefix.Length..].Split(Separator);
        return new HostLogEntry(Enum.Parse<LogEventLevel>(parts[0]), parts[1], parts[2]);
    }

    /// <summary>Whether the entry's message or exception text contains the fact.</summary>
    public bool Names(string fact) => Message.Contains(fact, StringComparison.Ordinal) || Exception.Contains(fact, StringComparison.Ordinal);
}
