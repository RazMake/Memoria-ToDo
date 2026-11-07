using System.Globalization;

namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>The program's five arguments, checked.</summary>
internal sealed record Options(string ReportsFolder, string TestOutputFile, TimeSpan Limit, bool IssueOpen, string TextFile)
{
    private const string Reports = "--reports";
    private const string TestOutput = "--test-output";
    private const string LimitSeconds = "--limit-seconds";
    private const string IssueOpenName = "--issue-open";
    private const string Text = "--text-file";

    /// <summary>Reads the arguments, each required once.</summary>
    /// <exception cref="ArgumentException">An argument is missing, repeated or unknown, or a value is invalid.</exception>
    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            var name = args[i];
            if (name is not (Reports or TestOutput or LimitSeconds or IssueOpenName or Text))
            {
                throw new ArgumentException($"Unknown argument '{name}'.");
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"The argument {name} has no value.");
            }

            if (!values.TryAdd(name, args[i + 1]))
            {
                throw new ArgumentException($"The argument {name} is repeated.");
            }
        }

        string Required(string name) =>
            values.TryGetValue(name, out var value) ? value : throw new ArgumentException($"The argument {name} is missing.");

        var textFile = Required(Text);
        var textFolder = Path.GetDirectoryName(Path.GetFullPath(textFile));
        if (textFolder is null || !Directory.Exists(textFolder) || Directory.Exists(textFile))
        {
            throw new ArgumentException($"The folder of the text file '{textFile}' doesn't exist.");
        }

        return new Options(
            Required(Reports),
            Required(TestOutput),
            ParseLimit(Required(LimitSeconds)),
            ParseIssueOpen(Required(IssueOpenName)),
            textFile);
    }

    private static TimeSpan ParseLimit(string value)
    {
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
            || seconds <= 0
            || seconds > TimeSpan.MaxValue.TotalSeconds)
        {
            throw new ArgumentException($"{LimitSeconds} must be a positive number with '.' for decimals, not '{value}'.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static bool ParseIssueOpen(string value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => throw new ArgumentException($"{IssueOpenName} must be 'true' or 'false', not '{value}'."),
    };
}
