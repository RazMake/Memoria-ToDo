using System.IO;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>An on-or-off setting file: one switch kept as <c>on</c> or <c>off</c> in a file on this device.</summary>
[Unit]
public class OnOffSettingFileTests
{
    private const string Folder = @"C:\Memoria\Device";
    private const string Path = Folder + @"\switch.txt";
    private const string Name = "The switch";

    private readonly FakeFileSystem _files = new();
    private readonly InMemoryLogSink _log = new();

    private OnOffSettingFile Create(bool whenMissing = false) =>
        new(_files, Path, whenMissing, Name, _log.CreateLogger());

    private void Save(string text) => _files.WriteFile(Path, Encoding.UTF8.GetBytes(text));

    private string Saved() => Encoding.UTF8.GetString(_files.ReadFile(Path));

    private IEnumerable<LogEvent> Warnings() => _log.Events.Where(e => e.Level == LogEventLevel.Warning);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Gives_the_value_for_a_missing_file_with_no_failure_and_logs_no_Warning(bool whenMissing)
    {
        var (on, failure) = Create(whenMissing).Read();

        Assert.Equal(whenMissing, on);
        Assert.Null(failure);
        Assert.Empty(Warnings());
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void Reads_on_or_off_from_the_file_whatever_the_value_for_a_missing_file(string text, bool expected)
    {
        Save(text);

        Assert.Equal((expected, (string?)null), Create(whenMissing: !expected).Read());
        Assert.Empty(Warnings());
    }

    [Theory]
    [InlineData("  on\r\n", true)]
    [InlineData("\toff \n", false)]
    public void Ignores_whitespace_around_the_text(string text, bool expected)
    {
        Save(text);

        Assert.Equal((expected, (string?)null), Create(whenMissing: !expected).Read());
    }

    [Theory]
    [InlineData("sometimes")]
    [InlineData("ON")]
    [InlineData("")]
    public void Counts_other_content_as_off_with_a_failure_naming_the_setting_and_logs_a_Warning(string text)
    {
        Save(text);

        var (on, failure) = Create(whenMissing: true).Read();

        Assert.False(on);
        Assert.StartsWith(Name, failure);
        Assert.Single(Warnings());
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Counts_a_file_that_cannot_be_read_as_off_with_a_failure_naming_the_setting_and_logs_a_Warning(Type failureType)
    {
        Save("on");
        _files.FailReadsUnder(Folder, (Exception)Activator.CreateInstance(failureType, "No access.")!);

        var (on, failure) = Create(whenMissing: true).Read();

        Assert.False(on);
        Assert.StartsWith(Name, failure);
        var warning = Assert.Single(Warnings());
        Assert.IsType(failureType, warning.Exception);
    }

    [Fact]
    public void Lets_any_other_failure_while_reading_pass_through()
    {
        Save("on");
        _files.FailReadsUnder(Folder, new InvalidOperationException("Unexpected."));

        var setting = Create();

        Assert.Throws<InvalidOperationException>(() => setting.Read());
    }

    [Theory]
    [InlineData(true, "on")]
    [InlineData(false, "off")]
    public void Writes_on_or_off_as_the_file_content_that_reads_back(bool on, string text)
    {
        var setting = Create(whenMissing: !on);

        setting.Write(on);

        Assert.Equal(text, Saved());
        Assert.Equal((on, (string?)null), setting.Read());
    }

    [Fact]
    public void Writing_replaces_what_the_file_held()
    {
        Save("sometimes, and a lot more text");
        var setting = Create();

        setting.Write(true);
        setting.Write(false);

        Assert.Equal("off", Saved());
    }

    [Fact]
    public void Fails_a_write_with_the_file_systems_exception_and_keeps_the_old_file()
    {
        Save("on");
        _files.FailWritesUnder(Folder);
        var setting = Create();

        Assert.Throws<IOException>(() => setting.Write(false));

        Assert.Equal("on", Saved());
    }

    [Fact]
    public void Creation_does_not_touch_the_file_system()
    {
        Create();

        Assert.Empty(_files.AllFilesUnder(Folder));
    }

    [Fact]
    public void Rejects_a_missing_file_system()
    {
        Assert.Throws<ArgumentNullException>(() => new OnOffSettingFile(null!, Path, false, Name, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_a_missing_path()
    {
        Assert.Throws<ArgumentNullException>(() => new OnOffSettingFile(_files, null!, false, Name, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_a_missing_name()
    {
        Assert.Throws<ArgumentNullException>(() => new OnOffSettingFile(_files, Path, false, null!, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_a_missing_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new OnOffSettingFile(_files, Path, false, Name, null!));
    }

    [Theory]
    [InlineData("switch.txt")]
    [InlineData(@"Device\switch.txt")]
    [InlineData("")]
    public void Rejects_a_path_that_is_not_fully_qualified(string path)
    {
        Assert.Throws<ArgumentException>(() => new OnOffSettingFile(_files, path, false, Name, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_an_empty_name()
    {
        Assert.Throws<ArgumentException>(() => new OnOffSettingFile(_files, Path, false, "", _log.CreateLogger()));
    }
}
