using System.Globalization;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// One entry as a row of the list shows it. The texts are made when asked, so a log of hundreds of thousands of entries holds
/// only these small rows until the rows on screen are drawn.
/// </summary>
internal sealed class LogRow
{
    private readonly TimeZoneInfo _zone;

    public LogRow(LogEntry entry, TimeZoneInfo zone)
    {
        Entry = entry;
        _zone = zone;
    }

    /// <summary>Orders rows newest first.</summary>
    public static IComparer<LogRow> NewestFirst { get; } = Comparer<LogRow>.Create((a, b) => b.Entry.Time.CompareTo(a.Entry.Time));

    public LogEntry Entry { get; }

    /// <summary>The time in this PC's local zone, with the date and milliseconds.</summary>
    public string TimeText => TimeZoneInfo.ConvertTime(Entry.Time, _zone).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>The level's name.</summary>
    public string SeverityText => Entry.Level.ToString();

    /// <summary>The severity, with a mark after it when the entry has an exception.</summary>
    public string SeverityCell => HasError ? $"{SeverityText} ⚠" : SeverityText;

    public string Component => Entry.Component;

    /// <summary>The first line of the message.</summary>
    public string Message
    {
        get
        {
            var message = Entry.Message;
            var end = message.AsSpan().IndexOfAny('\r', '\n');
            return end < 0 ? message : message[..end];
        }
    }

    public bool HasError => Entry.Error is not null;

    /// <summary>The whole message, then the exception's text with its stack trace.</summary>
    public string Details => HasError ? $"{Entry.Message}{Environment.NewLine}{Environment.NewLine}{Entry.Error}" : Entry.Message;

    /// <summary>The row as copied: a line of time, severity, component, and message, then the exception's text.</summary>
    public string CopyText =>
        $"{TimeText}  {SeverityText}  {Component}  {Entry.Message}" + (HasError ? $"{Environment.NewLine}{Entry.Error}" : "");

    /// <summary>Whether the message or the exception's text contains the text, ignoring case.</summary>
    public bool Contains(string text) =>
        Entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase) || Entry.Error?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Whether the entry is at the level or above.</summary>
    public bool IsAtLeast(LogEventLevel level) => Entry.Level >= level;
}
