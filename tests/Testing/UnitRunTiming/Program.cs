namespace Memoria.ToDo.Testing.UnitRunTiming;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            var decision = TimingIssue.Decide(ReadReports(options.ReportsFolder), File.ReadAllText(options.TestOutputFile), options.Limit, options.IssueOpen);
            if (decision.Text is not null)
            {
                File.WriteAllText(options.TextFile, decision.Text);
            }

            Console.Out.WriteLine(decision.Action.ToString().ToLowerInvariant());
            return 0;
        }
        catch (Exception failure) when (failure is ArgumentException or FormatException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(failure.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static List<string> ReadReports(string folder)
    {
        if (!Directory.Exists(folder))
        {
            throw new ArgumentException($"The reports folder '{folder}' doesn't exist.");
        }

        var reports = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.junit.xml", SearchOption.AllDirectories))
        {
            reports.Add(File.ReadAllText(file));
        }

        return reports.Count > 0 ? reports : throw new ArgumentException($"The reports folder '{folder}' holds no *.junit.xml file.");
    }
}
