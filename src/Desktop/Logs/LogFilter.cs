using Serilog.Events;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// Decides which log entries match: an entry matches when it matches the minimum level, the components, the time range, and
/// the text. It checks one entry at a time and keeps nothing, so a caller filters in one pass over what it read. The window
/// and the MCP server's log tool both filter with it.
/// </summary>
public sealed class LogFilter
{
    /// <summary>The levels a minimum can be, lowest first.</summary>
    public static IReadOnlyList<LogEventLevel> Levels { get; } =
        [LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal];

    private readonly LogEventLevel _minimumLevel;
    private readonly HashSet<string>? _components;
    private readonly DateTimeOffset? _from;
    private readonly DateTimeOffset? _to;
    private readonly string? _text;

    /// <summary>
    /// Makes a filter. <paramref name="components"/> are the names to keep, compared exactly; null keeps every component, and an
    /// empty list keeps none. <paramref name="from"/> is included and <paramref name="to"/> is not; either can be null for no
    /// limit. An empty or null <paramref name="text"/> matches every entry.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The minimum level isn't one of <see cref="Levels"/>.</exception>
    /// <exception cref="ArgumentNullException">A component name is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="from"/> is later than <paramref name="to"/>.</exception>
    public LogFilter(LogEventLevel minimumLevel, IEnumerable<string>? components, DateTimeOffset? from, DateTimeOffset? to, string? text)
    {
        if (minimumLevel < LogEventLevel.Debug || minimumLevel > LogEventLevel.Fatal)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLevel), minimumLevel, "The minimum level must be Debug, Information, Warning, Error, or Fatal.");
        }

        if (from > to)
        {
            throw new ArgumentException("From must not be later than To.", nameof(from));
        }

        if (components is not null)
        {
            _components = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in components)
            {
                ArgumentNullException.ThrowIfNull(name, nameof(components));
                _components.Add(name);
            }
        }

        _minimumLevel = minimumLevel;
        _from = from;
        _to = to;
        _text = string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Whether the entry matches all four parts.</summary>
    public bool Matches(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Level >= _minimumLevel
            && (_components is null || _components.Contains(entry.Component))
            && (_from is not { } from || entry.Time >= from)
            && (_to is not { } to || entry.Time < to)
            && (_text is null
                || entry.Message.Contains(_text, StringComparison.OrdinalIgnoreCase)
                || entry.Error?.Contains(_text, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>The components the entries hold, each name once, in name order ignoring case.</summary>
    public static IReadOnlyList<string> ComponentsOf(IEnumerable<LogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            names.Add(entry.Component);
        }

        return [.. names.Order(StringComparer.OrdinalIgnoreCase)];
    }
}
