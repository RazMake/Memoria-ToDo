using System.IO;
using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Formatting;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The safety net around the log file's format: writes the entry's line with the inner formatter, then replaces a
/// private key block and the user part of an address (a token, or a user name and password) with ***, wherever they sit in the line. A replacement never
/// crosses the end of a JSON string, so a line of JSON stays valid.
/// </summary>
internal sealed partial class RedactingFormatter(ITextFormatter inner) : ITextFormatter
{
    private const string Stars = "***";

    // A private key block up to its end line, or, with no end line, up to the end of the JSON string it sits in.
    // (?:[^"\\]|\\.) is one character of a JSON string, an escape such as \n or \" counting as one.
    [GeneratedRegex("""-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----(?:(?:[^"\\]|\\.)*?-----END [A-Z0-9 ]*PRIVATE KEY-----|(?:[^"\\]|\\.)*)""")]
    private static partial Regex PrivateKeyBlock();

    // The whole user part of scheme://userinfo@host, whether a token or user:password. An address without a scheme isn't touched.
    [GeneratedRegex("""://[^\s/@"\\]+@""")]
    private static partial Regex AddressUser();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);
        using var line = new StringWriter();
        inner.Format(logEvent, line);
        var redacted = PrivateKeyBlock().Replace(line.ToString(), Stars);
        output.Write(AddressUser().Replace(redacted, $"://{Stars}@"));
    }
}
