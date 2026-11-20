using Serilog.Events;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// One log entry as the window shows it. <paramref name="Level"/> is Serilog's. <paramref name="Component"/> is the entry's
/// SourceContext, or "Unknown" without one. <paramref name="Message"/> is the rendered message. <paramref name="Error"/> is the
/// exception's full text, or null without one.
/// </summary>
public sealed record LogEntry(DateTimeOffset Time, LogEventLevel Level, string Component, string Message, string? Error);
