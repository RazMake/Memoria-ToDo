using System.Globalization;
using System.Text.RegularExpressions;

namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>Reads the run's duration from the `duration:` line of saved `dotnet test` console output.</summary>
internal static partial class RunDurationReader
{
    private const string Label = "duration:";

    // A terminal escape sequence, such as a color code.
    [GeneratedRegex("\u001b\\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex EscapeSequence();

    /// <summary>Finds the one `duration:` line and returns its text, such as <c>4s 312ms</c>, and its value.</summary>
    /// <exception cref="FormatException">There isn't exactly one `duration:` line, or its value isn't in the summary's format.</exception>
    public static (string Text, TimeSpan Value) Read(string testOutput)
    {
        var plain = EscapeSequence().Replace(testOutput, "");
        string? text = null;
        foreach (var line in plain.AsSpan().EnumerateLines())
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(Label, StringComparison.Ordinal))
            {
                continue;
            }

            if (text is not null)
            {
                throw new FormatException("The console output has more than one 'duration:' line.");
            }

            text = trimmed[Label.Length..].Trim().ToString();
        }

        if (text is null)
        {
            throw new FormatException("The console output has no 'duration:' line.");
        }

        return (text, Parse(text));
    }

    private static TimeSpan Parse(string text)
    {
        var total = TimeSpan.Zero;
        var parts = 0;
        foreach (var range in text.AsSpan().Split(' '))
        {
            var part = text.AsSpan(range);
            if (part.IsEmpty)
            {
                continue;
            }

            var (unit, size) = part switch
            {
                _ when part.EndsWith("ms", StringComparison.Ordinal) => (TimeSpan.FromMilliseconds(1), 2),
                _ when part.EndsWith("s", StringComparison.Ordinal) => (TimeSpan.FromSeconds(1), 1),
                _ when part.EndsWith("m", StringComparison.Ordinal) => (TimeSpan.FromMinutes(1), 1),
                _ when part.EndsWith("h", StringComparison.Ordinal) => (TimeSpan.FromHours(1), 1),
                _ => (TimeSpan.Zero, 0),
            };
            if (size == 0 || !int.TryParse(part[..^size], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                throw InvalidDuration(text);
            }

            total += unit * count;
            parts++;
        }

        return parts == 0 ? throw InvalidDuration(text) : total;
    }

    private static FormatException InvalidDuration(string text) =>
        new($"The duration '{text}' isn't in the format of 'dotnet test', such as '312ms', '4s 312ms' or '1m 05s 012ms'.");
}
