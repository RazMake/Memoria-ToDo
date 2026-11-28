using System.Globalization;
using System.Xml;

namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>Reads the test cases of a JUnit XML report, streaming it without building a document.</summary>
internal static class JUnitReportReader
{
    private static readonly XmlReaderSettings Settings = new() { DtdProcessing = DtdProcessing.Prohibit };

    /// <summary>Adds each test case of the report to <paramref name="tests"/>.</summary>
    /// <exception cref="FormatException">The text isn't a JUnit report, or a test case has no name or no time in seconds.</exception>
    public static void ReadInto(string xml, List<TestTime> tests)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), Settings);
            var seenRoot = false;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!seenRoot)
                {
                    seenRoot = true;
                    if (reader.Name is not ("testsuites" or "testsuite"))
                    {
                        throw new FormatException($"A report isn't JUnit XML: its first element is <{reader.Name}>.");
                    }
                }
                else if (reader.Name == "testcase")
                {
                    tests.Add(ReadTestCase(reader));
                }
            }
        }
        catch (XmlException failure)
        {
            throw new FormatException("A report isn't valid XML: " + failure.Message, failure);
        }
    }

    private static TestTime ReadTestCase(XmlReader reader)
    {
        var name = reader.GetAttribute("name");
        if (string.IsNullOrEmpty(name))
        {
            throw new FormatException("A test case in a report has no name.");
        }

        if (!double.TryParse(reader.GetAttribute("time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || !double.IsFinite(seconds)
            || seconds < 0)
        {
            throw new FormatException($"The test case '{name}' has no time in seconds.");
        }

        var className = reader.GetAttribute("classname") ?? "";
        if (className.Length > 0 && name.Length > className.Length + 1 && name.StartsWith(className, StringComparison.Ordinal) && name[className.Length] == '.')
        {
            name = name[(className.Length + 1)..];
        }

        return new TestTime(className, name, (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero));
    }
}
