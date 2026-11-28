using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Reading one file without throwing for a bad file, and the skip rule for a file that can't be read.</summary>
[Unit]
public sealed class FileLoadTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "file-load");
    private static readonly string File = Path.Combine(Folder, "a.json");
    private readonly FakeFileSystem files = new();

    private void Seed(string text) => files.WriteFile(File, Encoding.UTF8.GetBytes(text));

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    // Read

    [Fact]
    public void Gives_the_absent_value_and_no_failure_for_a_missing_file()
    {
        var load = FileLoad<string>.Read(files, File, Text, "absent");

        Assert.Equal("absent", load.Value);
        Assert.False(load.Present);
        Assert.Null(load.Failure);
    }

    [Fact]
    public void Gives_the_parsed_value_for_a_good_file()
    {
        Seed("hello");

        var load = FileLoad<string>.Read(files, File, Text, "absent");

        Assert.Equal("hello", load.Value);
        Assert.True(load.Present);
        Assert.Null(load.Failure);
    }

    [Fact]
    public void Gives_the_absent_value_and_the_same_exception_as_the_failure_when_parsing_fails()
    {
        Seed("bad");
        var failure = new InvalidDataException("Damaged.");

        var load = FileLoad<string>.Read(files, File, _ => throw failure, "absent");

        Assert.Equal("absent", load.Value);
        Assert.True(load.Present);
        Assert.Same(failure, load.Failure);
    }

    [Fact]
    public void Wraps_an_IO_failure_from_reading_in_an_InvalidDataException_with_the_same_message()
    {
        Seed("x");
        files.FailReadsUnder(Folder, new IOException("Disk gone."));

        var load = FileLoad<string>.Read(files, File, Text, "absent");

        Assert.Equal("absent", load.Value);
        Assert.True(load.Present);
        Assert.NotNull(load.Failure);
        Assert.Equal("Disk gone.", load.Failure.Message);
    }

    [Fact]
    public void Wraps_an_access_failure_from_reading_in_an_InvalidDataException_with_the_same_message()
    {
        Seed("x");
        files.FailReadsUnder(Folder, new UnauthorizedAccessException("Denied."));

        var load = FileLoad<string>.Read(files, File, Text, "absent");

        Assert.True(load.Present);
        Assert.NotNull(load.Failure);
        Assert.Equal("Denied.", load.Failure.Message);
    }

    [Fact]
    public void Wraps_an_IO_failure_from_parsing_in_an_InvalidDataException_with_the_same_message()
    {
        Seed("x");

        var load = FileLoad<string>.Read(files, File, _ => throw new IOException("Cut off."), "absent");

        Assert.NotNull(load.Failure);
        Assert.Equal("Cut off.", load.Failure.Message);
    }

    [Fact]
    public void Lets_any_other_exception_from_reading_pass_through()
    {
        Seed("x");
        var failure = new ObjectDisposedException("files");
        files.FailReadsUnder(Folder, failure);

        Assert.Same(failure, Assert.Throws<ObjectDisposedException>(() => FileLoad<string>.Read(files, File, Text, "absent")));
    }

    [Fact]
    public void Lets_any_other_exception_from_parsing_pass_through()
    {
        Seed("x");
        var failure = new InvalidOperationException("Bug.");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => FileLoad<string>.Read(files, File, _ => throw failure, "absent")));
    }

    // Require

    [Fact]
    public void Requires_the_value_of_a_good_file()
    {
        Seed("hello");

        Assert.Equal("hello", FileLoad<string>.Read(files, File, Text, "absent").Require());
    }

    [Fact]
    public void Requires_the_absent_value_of_a_missing_file()
    {
        Assert.Equal("absent", FileLoad<string>.Read(files, File, Text, "absent").Require());
    }

    [Fact]
    public void Requires_by_throwing_an_InvalidDataException_around_the_failure()
    {
        Seed("bad");
        var failure = new InvalidDataException("Damaged.");
        var load = FileLoad<string>.Read(files, File, _ => throw failure, "absent");

        var thrown = Assert.Throws<InvalidDataException>(load.Require);

        Assert.Equal("Damaged.", thrown.Message);
    }
}
