using System.Text;

namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>
/// Inputs shaped like the real ones: xUnit.net v3's JUnit report and the end of `dotnet test`'s console output,
/// both read from a real run of a small throwaway project.
/// </summary>
internal static class Samples
{
    public sealed record Case(string ClassName, string ShortName, string Seconds)
    {
        public string FullName => $"{ClassName}.{ShortName}";
    }

    public static Case TestCase(string shortName, string seconds, string className = "Sample.Tests.Orders") =>
        new(className, shortName, seconds);

    /// <summary>One project's JUnit report.</summary>
    public static string Report(params Case[] cases)
    {
        var xml = new StringBuilder();
        xml.Append("<testsuites name=\"Test results\" tests=\"").Append(cases.Length).Append("\" failures=\"0\" errors=\"0\" disabled=\"0\" time=\"1.521000\" timestamp=\"2026-11-07T18:03:54\">");
        xml.Append("<testsuite name=\"C:\\repo\\bin\\Debug\\net11.0\\sample.dll\" tests=\"").Append(cases.Length).Append("\" failures=\"0\" errors=\"0\" disabled=\"0\" skipped=\"0\" time=\"1.521000\" timestamp=\"2026-11-07T18:03:54\" hostname=\"HOST\">");
        foreach (var c in cases)
        {
            xml.Append("<testcase name=\"").Append(c.FullName).Append("\" time=\"").Append(c.Seconds)
               .Append("\" classname=\"").Append(c.ClassName).Append("\" />");
        }
        xml.Append("</testsuite></testsuites>");
        return xml.ToString();
    }

    /// <summary>A report whose test cases carry the given attribute text instead of a time.</summary>
    public static string ReportWithTestCaseAttributes(string attributes) =>
        "<testsuites name=\"Test results\"><testsuite name=\"sample.dll\"><testcase " + attributes + " /></testsuite></testsuites>";

    /// <summary>The console output of a passing `dotnet test` run, ending with the run summary.</summary>
    public static string Output(string duration) =>
        "Running tests from C:\\repo\\bin\\Debug\\net11.0\\sample.dll (net11.0|x64)\r\n" +
        "C:\\repo\\bin\\Debug\\net11.0\\sample.dll (net11.0|x64) passed [+4/x0/?0] (2s 019ms)\r\n" +
        "\r\n" +
        "  In process file artifacts produced:\r\n" +
        "    - C:\\repo\\res\\HOST_2026-11-07.junit.xml\r\n" +
        "\r\n" +
        "Test run summary: Passed!\r\n" +
        "  total: 4\r\n" +
        "  failed: 0\r\n" +
        "  succeeded: 4\r\n" +
        "  skipped: 0\r\n" +
        $"  duration: {duration}\r\n";
}
