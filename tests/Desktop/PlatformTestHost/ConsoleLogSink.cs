using Serilog.Core;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.PlatformTestHost;

/// <summary>
/// The logger the platform runs the app with. It prints each entry on a line of standard output, so a test sees what
/// the platform logged and whether the entry came before the process ended. The line is
/// <c>log-entry &lt;Level&gt;\u001f&lt;rendered message&gt;\u001f&lt;exception text&gt;</c>, with line breaks in the texts
/// replaced by spaces; the tests' HostLogEntry reads it back. Console output is flushed on every line, so an entry
/// written just before the process ends isn't lost.
/// </summary>
internal sealed class ConsoleLogSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var message = OneLine(logEvent.RenderMessage());
        var exception = OneLine(logEvent.Exception?.ToString() ?? "");
        Console.Out.WriteLine($"log-entry {logEvent.Level}\u001f{message}\u001f{exception}");
    }

    private static string OneLine(string text) => text.Replace("\r", " ").Replace("\n", " ");
}
