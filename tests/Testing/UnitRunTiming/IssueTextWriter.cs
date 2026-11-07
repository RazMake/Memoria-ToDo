using System.Globalization;
using System.Text;

namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>Writes the Markdown text of the timing issue, its notes and its closing comment.</summary>
internal static class IssueTextWriter
{
    /// <summary>GitHub's limit for the body of an issue or a comment, in characters.</summary>
    public const int MaxLength = 65_536;

    // The longest closing line: "_9999999999 faster tests left out._" with its line break.
    private const int ReservedForLeftOut = 48;

    /// <summary>One line for the closing comment.</summary>
    public static string Closing(string duration, TimeSpan limit) =>
        $"The unit run took {duration}, at or under the limit of {Seconds(limit)}.";

    /// <summary>The duration, the limit and a table of the tests, slowest first, cut to fit GitHub's body limit.</summary>
    /// <param name="tests">The tests; this method sorts the list.</param>
    public static string Slow(string duration, TimeSpan limit, List<TestTime> tests)
    {
        tests.Sort(SlowestFirst);

        var text = new StringBuilder(Math.Min(MaxLength, 256 + tests.Count * 96));
        text.Append("The unit run took ").Append(duration).Append(", over the limit of ").Append(Seconds(limit)).Append(".\n\n")
            .Append("| Class | Test | Time (ms) |\n| --- | --- | ---: |");

        for (var i = 0; i < tests.Count; i++)
        {
            var before = text.Length;
            AppendRow(text, tests[i]);
            if (text.Length + ReservedForLeftOut > MaxLength)
            {
                text.Length = before;
                text.Append("\n\n_").Append((tests.Count - i).ToString(CultureInfo.InvariantCulture)).Append(" faster tests left out._");
                break;
            }
        }

        return text.ToString();
    }

    private static int SlowestFirst(TestTime a, TestTime b)
    {
        var byTime = b.Milliseconds.CompareTo(a.Milliseconds);
        if (byTime != 0)
        {
            return byTime;
        }

        var byName = string.CompareOrdinal(a.ClassName, b.ClassName);
        return byName != 0 ? byName : string.CompareOrdinal(a.Name, b.Name);
    }

    private static void AppendRow(StringBuilder text, TestTime test)
    {
        text.Append("\n| ");
        AppendCell(text, test.ClassName);
        text.Append(" | ");
        AppendCell(text, test.Name);
        text.Append(" | ").Append(test.Milliseconds.ToString(CultureInfo.InvariantCulture)).Append(" |");
    }

    private static void AppendCell(StringBuilder text, string value)
    {
        foreach (var c in value)
        {
            if (c == '|')
            {
                text.Append('\\');
            }

            text.Append(c is '\r' or '\n' ? ' ' : c);
        }
    }

    private static string Seconds(TimeSpan limit) =>
        limit.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s";
}
