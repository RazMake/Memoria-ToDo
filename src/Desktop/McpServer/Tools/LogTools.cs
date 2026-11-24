using System.ComponentModel;
using System.Globalization;
using Memoria.ToDo.Desktop.Logs;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.McpServer.Tools;

/// <summary>The tool that reads the app's own log, filtered by the same <see cref="LogFilter"/> the Logs window uses.</summary>
internal sealed class LogTools(ToolRunner run, ILogSource logs, TimeProvider time, ILogger logger)
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 500;
    private static readonly string[] TimeFormats = ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];

    [McpServerTool(ReadOnly = true), Description("Reads the app's own log, newest entry first, to find out why something failed. Answers the matching entries, how many matched, how many the log holds, the components the log has, and the time now.")]
    public Task<string> ReadLog(
        [Description("The least severe level to include: Debug, Information, Warning, Error, or Fatal. Information when left out.")] string? level = null,
        [Description("Only entries of these components, named exactly. The error for an unknown name lists the components.")] string[]? components = null,
        [Description("Only entries at or after this moment, as ISO 8601 with its offset or Z, such as 2026-11-23T10:40:00-08:00.")] string? from = null,
        [Description("Only entries before this moment, in the same form.")] string? to = null,
        [Description("Only entries whose message or error contains this text, ignoring case.")] string? text = null,
        [Description("At most this many entries, from 1 to 500. 100 when left out.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        run.RunAsync(cancellationToken, async token =>
        {
            var minimum = level is null ? LogEventLevel.Information : LevelOf(level);
            var start = from is null ? (DateTimeOffset?)null : TimeOf(from, nameof(from));
            var end = to is null ? (DateTimeOffset?)null : TimeOf(to, nameof(to));
            var count = limit ?? DefaultLimit;
            if (count is < 1 or > MaxLimit)
            {
                throw new ArgumentException($"The limit must be from 1 to {MaxLimit}.");
            }

            _ = new LogFilter(minimum, null, start, end, text); // refuses From later than To before the log is read
            var read = await logs.ReadAsync(token).ConfigureAwait(false);
            var known = LogFilter.ComponentsOf(read.Entries);
            if (components is not null)
            {
                RequireKnown(components, known);
            }

            var filter = new LogFilter(minimum, components, start, end, text);
            var (newest, matched) = NewestMatches(read.Entries, filter, count);
            logger.Debug(
                "read_log: level {Level}, from {From}, to {To}, components {Components}, limit {Limit}; read {Read}, matched {Matched}, returned {Returned}",
                minimum, start, end, components is null ? "all" : string.Join(", ", components), count, read.Entries.Count, matched, newest.Length);
            return new
            {
                now = TimeText(time.GetLocalNow()),
                entries = newest.Select(entry => new
                {
                    time = TimeText(TimeZoneInfo.ConvertTime(entry.Time, time.LocalTimeZone)),
                    level = entry.Level.ToString(),
                    component = entry.Component,
                    message = entry.Message,
                    error = entry.Error,
                }).ToList(),
                matched,
                total = read.Entries.Count,
                components = known,
                unreadableLines = read.UnreadableLines,
                writeProblem = read.WriteProblem,
            };
        });

    // One pass; only the newest `limit` matches are kept, in a min-heap whose root is the oldest of them.
    private static (LogEntry[] Newest, int Matched) NewestMatches(IReadOnlyList<LogEntry> entries, LogFilter filter, int limit)
    {
        var heap = new PriorityQueue<LogEntry, DateTimeOffset>(limit + 1);
        var matched = 0;
        foreach (var entry in entries)
        {
            if (!filter.Matches(entry))
            {
                continue;
            }

            matched++;
            heap.Enqueue(entry, entry.Time);
            if (heap.Count > limit)
            {
                heap.Dequeue();
            }
        }

        var newest = new LogEntry[heap.Count];
        for (var index = newest.Length - 1; index >= 0; index--)
        {
            newest[index] = heap.Dequeue();
        }

        return (newest, matched);
    }

    private static void RequireKnown(string[] components, IReadOnlyList<string> known)
    {
        if (components.Length == 0)
        {
            throw new ArgumentException($"Name at least one component, or leave components out. The log has: {string.Join(", ", known)}.");
        }

        var unknown = components.FirstOrDefault(name => !known.Contains(name, StringComparer.Ordinal));
        if (unknown is not null)
        {
            throw new ArgumentException($"The log has no component named {unknown}. It has: {string.Join(", ", known)}.");
        }
    }

    private static LogEventLevel LevelOf(string name) =>
        LogFilter.Levels.Cast<LogEventLevel?>().FirstOrDefault(level => string.Equals(level.ToString(), name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"The level must be one of: {string.Join(", ", LogFilter.Levels)}.");

    private static DateTimeOffset TimeOf(string text, string parameter) =>
        DateTimeOffset.TryParseExact(text.EndsWith('Z') ? text[..^1] + "+00:00" : text, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
            ? moment
            : throw new ArgumentException($"{parameter} must be an ISO 8601 date and time with its offset or Z, such as 2026-11-23T10:40:00-08:00.");

    private static string TimeText(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
}
