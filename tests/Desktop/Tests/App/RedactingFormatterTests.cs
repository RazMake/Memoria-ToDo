using System.IO;
using System.Text.Json;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The safety net around the log file's format: after the compact JSON formatter writes an entry's line, secrets in it
/// are replaced with ***, wherever they are, and the line stays one valid JSON object.
/// </summary>
[Unit]
public sealed class RedactingFormatterTests
{
    private const string KeyBody = "b3BlbnNzaC1rZXktdjEAAAAABG5vbmU";
    private const string KeyBlock = $"-----BEGIN OPENSSH PRIVATE KEY-----\n{KeyBody}\nAAAAB3NzaC1yc2E\n-----END OPENSSH PRIVATE KEY-----";

    /// <summary>The line the redacting formatter writes for the one entry the action logs.</summary>
    private static string Redacted(Action<ILogger> log) => Format(new RedactingFormatter(new CompactJsonFormatter()), Entry(log));

    private static LogEvent Entry(Action<ILogger> log)
    {
        var sink = new InMemoryLogSink();
        log(sink.CreateLogger());
        return Assert.Single(sink.Events);
    }

    private static string Format(ITextFormatter formatter, LogEvent entry)
    {
        using var writer = new StringWriter();
        formatter.Format(entry, writer);
        return writer.ToString();
    }

    /// <summary>Parses the line as one JSON object, and checks it is one line.</summary>
    private static JsonElement Parsed(string line)
    {
        Assert.DoesNotContain('\n', line.TrimEnd('\r', '\n'));
        return JsonDocument.Parse(line).RootElement.Clone();
    }

    [Fact]
    public void Replaces_a_private_key_block_with_stars_and_keeps_the_text_around_it()
    {
        var line = Redacted(log => log.Information("Loaded {Key} for the device", KeyBlock));

        Assert.DoesNotContain(KeyBody, line);
        Assert.DoesNotContain("AAAAB3NzaC1yc2E", line);
        var key = Parsed(line).GetProperty("Key").GetString();
        Assert.Contains("***", key);
        Assert.DoesNotContain("PRIVATE KEY", key);
        Assert.Equal("Loaded {Key} for the device", Parsed(line).GetProperty("@mt").GetString());
    }

    [Fact]
    public void Replaces_a_private_key_block_that_has_no_end_line_up_to_the_end_of_its_text()
    {
        var cutOff = "-----BEGIN RSA PRIVATE KEY-----\n" + KeyBody + "\nAAAAB3NzaC1yc2E";

        var line = Redacted(log => log.Information("Started with {Key} and {Other}", "before " + cutOff, "untouched"));

        Assert.DoesNotContain(KeyBody, line);
        Assert.DoesNotContain("AAAAB3NzaC1yc2E", line);
        var parsed = Parsed(line);
        Assert.StartsWith("before ", parsed.GetProperty("Key").GetString());
        Assert.Contains("***", parsed.GetProperty("Key").GetString());
        Assert.Equal("untouched", parsed.GetProperty("Other").GetString());
    }

    [Fact]
    public void Keeps_the_line_valid_when_a_key_without_an_end_line_holds_a_quote_or_a_backslash()
    {
        var cutOff = "-----BEGIN PRIVATE KEY-----\nAAA\"BBB\\CCC";

        var line = Redacted(log => log.Information("Key {Key} then {Other}", cutOff, "untouched"));

        var parsed = Parsed(line);
        Assert.DoesNotContain("BBB", line);
        Assert.DoesNotContain("CCC", line);
        Assert.Equal("untouched", parsed.GetProperty("Other").GetString());
    }

    [Theory]
    [InlineData("https://ghp_t0kenValue123@example.com/owner/repo.git", "ghp_t0kenValue123", "https://***@example.com/owner/repo.git")]
    [InlineData("ssh://git:hunter2@example.com/owner/repo.git", "hunter2", "ssh://***@example.com/owner/repo.git")]
    [InlineData("ssh://git@github.com/owner/repo.git", "//git@", "ssh://***@github.com/owner/repo.git")]
    [InlineData("https://user:p%40ss-w0rd@example.com:8443/path", "p%40ss-w0rd", "https://***@example.com:8443/path")]
    public void Replaces_the_whole_user_part_of_an_address_with_a_scheme_with_stars_and_keeps_the_rest_of_it(string address, string secret, string expected)
    {
        var line = Redacted(log => log.Information("Cloning {Address}", address));

        Assert.DoesNotContain(secret, line);
        Assert.Equal(expected, Parsed(line).GetProperty("Address").GetString());
    }

    [Theory]
    [InlineData("ghp_t0kenValue123", "https://ghp_t0kenValue123@example.com/owner/repo.git")]
    [InlineData("hunter2", "ssh://git:hunter2@example.com/owner/repo.git")]
    public void Redacts_an_address_inside_an_exception_text_where_the_json_escapes_the_quotes_around_it(string secret, string address)
    {
        var failure = new InvalidOperationException($"fatal: unable to access \"{address}\": the host said \"no\"\nnext line");

        var line = Redacted(log => log.Error(failure, "Clone failed"));

        Assert.DoesNotContain(secret, line);
        var text = Parsed(line).GetProperty("@x").GetString();
        Assert.Contains("***@example.com/owner/repo.git", text);
        Assert.Contains("the host said \"no\"", text);
        Assert.Contains("next line", text);
    }

    [Fact]
    public void Keeps_the_user_name_of_an_address_without_a_scheme()
    {
        var line = Redacted(log => log.Information("Cloning {Address}", "git@github.com:owner/repo.git"));

        Assert.Equal("git@github.com:owner/repo.git", Parsed(line).GetProperty("Address").GetString());
    }

    [Fact]
    public void Keeps_the_user_name_of_an_address_without_a_scheme_inside_an_exception_text()
    {
        var failure = new InvalidOperationException("fatal: could not read from \"git@github.com:owner/repo.git\"");

        var line = Redacted(log => log.Error(failure, "Fetch failed"));

        Assert.Contains("git@github.com:owner/repo.git", Parsed(line).GetProperty("@x").GetString());
    }

    [Fact]
    public void Covers_a_secret_in_the_message_a_value_and_the_exception_text_alike()
    {
        const string Address = "ssh://git:hunter2@example.com/repo.git";

        var inMessage = Redacted(log => log.Information("Cloning " + Address));
        var inValue = Redacted(log => log.Information("Cloning {Address}", Address));
        var inException = Redacted(log => log.Error(new InvalidOperationException($"git failed for {Address}\n{KeyBlock}"), "Clone failed"));

        foreach (var line in new[] { inMessage, inValue, inException })
        {
            Assert.DoesNotContain("hunter2", line);
            Assert.Contains("***", Parsed(line).ToString());
        }

        Assert.DoesNotContain(KeyBody, inException);
        Assert.Contains("***", Parsed(inException).GetProperty("@x").GetString());
    }

    [Fact]
    public void Writes_one_valid_json_line_for_an_entry_with_a_secret_in_a_multi_line_exception()
    {
        var line = Redacted(log => log.Error(
            new InvalidOperationException("first line\nssh://git:hunter2@example.com/x\n" + KeyBlock + "\nlast line"), "Failed"));

        var parsed = Parsed(line);
        Assert.Equal("Failed", parsed.GetProperty("@mt").GetString());
        Assert.Contains("last line", parsed.GetProperty("@x").GetString());
        Assert.Single(line.TrimEnd('\r', '\n').Split('\n'));
    }

    [Theory]
    [InlineData("Fetching https://example.com:8443/path from the remote")]
    [InlineData("Contact is someone@example.com, port 22")]
    [InlineData("A key is named by its fingerprint SHA256:abc123")]
    [InlineData("Mentions -----BEGIN PUBLIC KEY----- of a public key")]
    public void Leaves_an_entry_without_a_secret_exactly_as_the_compact_formatter_wrote_it(string text)
    {
        var failure = new InvalidOperationException("plain failure");
        var entry = Entry(log => log.Error(failure, "Entry {Text}", text));

        Assert.Equal(
            Format(new CompactJsonFormatter(), entry),
            Format(new RedactingFormatter(new CompactJsonFormatter()), entry));
    }
}
