using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// How tests read the entries an <see cref="InMemoryLogSink"/> kept. A test checks an entry's level and the facts it
/// names, never its wording: an entry names a fact when its rendered message, the text a person reads, contains it.
/// </summary>
public static class LogEventChecks
{
    /// <summary>The entries logged after the first <paramref name="skip"/>, such as those an operation added.</summary>
    public static IReadOnlyList<LogEvent> After(this InMemoryLogSink sink, int skip) => [.. sink.Events.Skip(skip)];

    /// <summary>Whether the entry's rendered message contains the fact.</summary>
    public static bool Names(this LogEvent entry, string fact) => entry.RenderMessage().Contains(fact, StringComparison.Ordinal);

    /// <summary>The entries at the level that name every fact.</summary>
    public static IReadOnlyList<LogEvent> At(this IEnumerable<LogEvent> entries, LogEventLevel level, params string[] facts) =>
        [.. entries.Where(entry => entry.Level == level && facts.All(entry.Names))];

    /// <summary>The one entry at the level that names every fact; fails when there are none or several.</summary>
    public static LogEvent SingleAt(this IEnumerable<LogEvent> entries, LogEventLevel level, params string[] facts)
    {
        var found = entries.At(level, facts);
        Assert.True(
            found.Count == 1,
            $"Expected one {level} entry naming [{string.Join(", ", facts)}], found {found.Count}. Entries: {Describe(entries)}");
        return found[0];
    }

    /// <summary>Asserts no entry is at Warning or above, such as for an expected outcome that isn't a failure.</summary>
    public static void AssertNoProblems(this IEnumerable<LogEvent> entries) =>
        Assert.True(!entries.Any(entry => entry.Level >= LogEventLevel.Warning), "Unexpected problem entries: " + Describe(entries));

    /// <summary>Everything an entry holds that a reader of the log could see: its message, every value, and its exception's text.</summary>
    public static string EverythingIn(this LogEvent entry) =>
        string.Join('\n', [entry.RenderMessage(), .. entry.Properties.Select(p => p.Key + "=" + p.Value), entry.Exception?.ToString() ?? ""]);

    /// <summary>Whether the entry has a Boolean value equal to <paramref name="expected"/>, such as a file's "needs a newer app".</summary>
    public static bool HasFlag(this LogEvent entry, bool expected) =>
        entry.Properties.Values.Any(value => value is ScalarValue { Value: bool flag } && flag == expected);

    /// <summary>The entry's value for the named property, or null when it has none or it isn't a single value.</summary>
    public static object? Property(this LogEvent entry, string name) =>
        entry.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar ? scalar.Value : null;

    /// <summary>Whether the entry's exception is <paramref name="failure"/>, or has it somewhere in its chain of inner exceptions.</summary>
    public static bool HasException(this LogEvent entry, Exception failure)
    {
        for (var e = entry.Exception; e is not null; e = e.InnerException)
        {
            if (ReferenceEquals(e, failure))
                return true;
        }

        return false;
    }

    private static string Describe(IEnumerable<LogEvent> entries) =>
        string.Join(" | ", entries.Select(entry => $"{entry.Level}: {entry.RenderMessage()}"));
}
