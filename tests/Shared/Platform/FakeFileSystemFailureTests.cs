using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The fake's test aid: telling it to fail writes under a path.</summary>
[Unit]
public sealed class FakeFileSystemFailureTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "fake-failures");
    private readonly FakeFileSystem files = new();

    private string In(params string[] parts) => Path.Combine([Root, .. parts]);

    private void Write(string path, string text) => files.WriteFile(path, Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Fails_a_write_under_the_path_with_an_io_exception()
    {
        files.FailWritesUnder(In("tasks"));

        Assert.Throws<IOException>(() => Write(In("tasks", "a.txt"), "x"));
    }

    [Fact]
    public void Fails_a_write_in_a_folder_below_the_path()
    {
        files.FailWritesUnder(In("tasks"));

        Assert.Throws<IOException>(() => Write(In("tasks", "deeper", "a.txt"), "x"));
    }

    [Fact]
    public void Leaves_an_existing_file_under_the_path_whole_when_its_write_fails()
    {
        Write(In("tasks", "a.txt"), "old");
        files.FailWritesUnder(In("tasks"));

        Assert.Throws<IOException>(() => Write(In("tasks", "a.txt"), "new"));

        Assert.Equal("old", Encoding.UTF8.GetString(files.ReadFile(In("tasks", "a.txt"))));
    }

    [Fact]
    public void Creates_no_file_when_a_write_fails()
    {
        files.FailWritesUnder(In("tasks"));

        Assert.Throws<IOException>(() => Write(In("tasks", "a.txt"), "x"));

        Assert.False(files.FileExists(In("tasks", "a.txt")));
    }

    [Fact]
    public void Still_writes_outside_the_path()
    {
        files.FailWritesUnder(In("tasks"));

        Write(In("other", "a.txt"), "x");

        Assert.True(files.FileExists(In("other", "a.txt")));
    }

    [Fact]
    public void Does_not_treat_a_folder_with_the_same_prefix_as_under_the_path()
    {
        files.FailWritesUnder(In("tasks"));

        Write(In("tasks2", "a.txt"), "x");

        Assert.True(files.FileExists(In("tasks2", "a.txt")));
    }

    [Fact]
    public void Fails_a_move_into_the_path_and_keeps_the_source()
    {
        Write(In("a.txt"), "x");
        files.FailWritesUnder(In("tasks"));

        Assert.Throws<IOException>(() => files.MoveFile(In("a.txt"), In("tasks", "a.txt")));

        Assert.True(files.FileExists(In("a.txt")));
        Assert.False(files.FileExists(In("tasks", "a.txt")));
    }

    [Fact]
    public void Fails_a_read_under_the_path_with_an_io_exception_while_the_file_still_exists()
    {
        Write(In("tasks", "a.txt"), "x");
        files.FailReadsUnder(In("tasks"));

        Assert.Throws<IOException>(() => files.ReadFile(In("tasks", "a.txt")));
        Assert.True(files.FileExists(In("tasks", "a.txt")));
    }

    [Fact]
    public void Still_reads_files_outside_the_path_that_fails_reads()
    {
        Write(In("other", "a.txt"), "x");
        files.FailReadsUnder(In("tasks"));

        Assert.Equal("x", Encoding.UTF8.GetString(files.ReadFile(In("other", "a.txt"))));
    }

    [Fact]
    public void Fails_deleting_a_file_under_the_path_and_keeps_it()
    {
        Write(In("tasks", "a.txt"), "x");
        files.FailDeletesUnder(In("tasks"));

        Assert.Throws<IOException>(() => files.DeleteFile(In("tasks", "a.txt")));

        Assert.True(files.FileExists(In("tasks", "a.txt")));
    }

    [Fact]
    public void Fails_deleting_a_folder_under_the_path_and_keeps_its_files()
    {
        Write(In("tasks", "deeper", "a.txt"), "x");
        files.FailDeletesUnder(In("tasks"));

        Assert.Throws<IOException>(() => files.DeleteFolder(In("tasks", "deeper")));
        Assert.Throws<IOException>(() => files.DeleteFolder(In("tasks")));

        Assert.True(files.FileExists(In("tasks", "deeper", "a.txt")));
    }

    [Fact]
    public void Still_deletes_outside_the_path_that_fails_deletes()
    {
        Write(In("other", "a.txt"), "x");
        files.FailDeletesUnder(In("tasks"));

        files.DeleteFile(In("other", "a.txt"));

        Assert.False(files.FileExists(In("other", "a.txt")));
    }

    [Fact]
    public void Works_again_after_the_failures_stop()
    {
        Write(In("tasks", "a.txt"), "x");
        files.FailWritesUnder(In("tasks"));
        files.FailReadsUnder(In("tasks"));
        files.FailDeletesUnder(In("tasks"));

        files.StopFailing();

        Write(In("tasks", "b.txt"), "y");
        Assert.Equal("x", Encoding.UTF8.GetString(files.ReadFile(In("tasks", "a.txt"))));
        files.DeleteFile(In("tasks", "a.txt"));
        Assert.False(files.FileExists(In("tasks", "a.txt")));
    }

    [Fact]
    public void Still_reads_and_lists_files_under_the_path()
    {
        Write(In("tasks", "a.txt"), "x");
        files.FailWritesUnder(In("tasks"));

        Assert.Equal("x", Encoding.UTF8.GetString(files.ReadFile(In("tasks", "a.txt"))));
        Assert.Equal([In("tasks", "a.txt")], files.ListFiles(In("tasks")));
    }
}
