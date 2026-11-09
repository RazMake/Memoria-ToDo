using Memoria.ToDo.Shared.Platform;
using Xunit;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// What every <see cref="IFileSystem"/> must do, as tests. A class derived from it supplies the file system and
/// a folder it may use, and carries the kind attribute: [Unit] for <see cref="FakeFileSystem"/>, [Integration]
/// for an app's real implementation, whose derived class gives each test a fresh real folder and deletes it.
/// </summary>
public abstract class FileSystemContract
{
    /// <summary>The file system under test.</summary>
    protected abstract IFileSystem Files { get; }

    /// <summary>A full path of a folder the test may use; nothing is in it at the start of a test.</summary>
    protected abstract string Root { get; }

    private string At(params string[] parts) => Path.Combine([Root, .. parts]);

    private static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    private string[] Listed(string folder) => [.. Files.ListFiles(folder).Order(StringComparer.Ordinal)];

    [Fact]
    public void Reads_back_the_whole_file_that_was_written()
    {
        byte[] content = [0, 1, 2, 255, 10, 13];

        Files.WriteFile(At("a.json"), content);

        Assert.Equal(content, Files.ReadFile(At("a.json")));
    }

    [Fact]
    public void Reads_back_an_empty_file()
    {
        Files.WriteFile(At("empty.json"), []);

        Assert.True(Files.FileExists(At("empty.json")));
        Assert.Empty(Files.ReadFile(At("empty.json")));
    }

    [Fact]
    public void Reads_back_a_file_of_one_megabyte()
    {
        var content = new byte[1024 * 1024];
        new Random(7).NextBytes(content);

        Files.WriteFile(At("big.png"), content);

        Assert.Equal(content, Files.ReadFile(At("big.png")));
    }

    [Fact]
    public void Changing_the_bytes_that_were_read_does_not_change_the_file()
    {
        Files.WriteFile(At("a.json"), Bytes("abc"));

        Files.ReadFile(At("a.json"))[0] = 0;

        Assert.Equal(Bytes("abc"), Files.ReadFile(At("a.json")));
    }

    [Fact]
    public void Reports_whether_a_file_exists()
    {
        Assert.False(Files.FileExists(At("a.json")));

        Files.WriteFile(At("a.json"), Bytes("x"));

        Assert.True(Files.FileExists(At("a.json")));
    }

    [Fact]
    public void Does_not_report_a_folder_as_a_file()
    {
        Files.WriteFile(At("folder", "a.json"), Bytes("x"));

        Assert.False(Files.FileExists(At("folder")));
    }

    [Fact]
    public void Throws_FileNotFoundException_when_reading_a_missing_file()
    {
        Assert.Throws<FileNotFoundException>(() => Files.ReadFile(At("missing.json")));
    }

    [Fact]
    public void Writing_replaces_the_whole_file()
    {
        Files.WriteFile(At("a.json"), Bytes("a long first version"));

        Files.WriteFile(At("a.json"), Bytes("short"));

        Assert.Equal(Bytes("short"), Files.ReadFile(At("a.json")));
    }

    [Fact]
    public void Writing_creates_the_missing_folders()
    {
        Files.WriteFile(At("one", "two", "a.json"), Bytes("x"));

        Assert.Equal(Bytes("x"), Files.ReadFile(At("one", "two", "a.json")));
    }

    [Fact]
    public void A_finished_write_leaves_no_temporary_file()
    {
        Files.WriteFile(At("a.json"), Bytes("x"));

        Assert.Equal([At("a.json")], Listed(Root));
    }

    [Fact]
    public void A_write_replaces_the_temporary_file_a_crash_left_beside_the_target()
    {
        Files.WriteFile(At("a.json.tmp"), Bytes("half"));

        Files.WriteFile(At("a.json"), Bytes("whole"));

        Assert.Equal(Bytes("whole"), Files.ReadFile(At("a.json")));
        Assert.False(Files.FileExists(At("a.json.tmp")));
    }

    [Fact]
    public void A_write_leaves_the_other_files_alone()
    {
        Files.WriteFile(At("a.json"), Bytes("a"));
        Files.WriteFile(At("b.json"), Bytes("b"));

        Files.WriteFile(At("a.json"), Bytes("a2"));

        Assert.Equal(Bytes("b"), Files.ReadFile(At("b.json")));
    }

    [Fact]
    public void Lists_the_full_paths_of_the_files_directly_in_a_folder()
    {
        Files.WriteFile(At("b.json"), Bytes("b"));
        Files.WriteFile(At("a.json"), Bytes("a"));

        Assert.Equal([At("a.json"), At("b.json")], Listed(Root));
    }

    [Fact]
    public void Listing_skips_the_files_in_subfolders_and_in_other_folders()
    {
        Files.WriteFile(At("tasks", "a.json"), Bytes("a"));
        Files.WriteFile(At("tasks", "nested", "b.json"), Bytes("b"));
        Files.WriteFile(At("tasks2", "c.json"), Bytes("c"));

        Assert.Equal([At("tasks", "a.json")], Listed(At("tasks")));
    }

    [Fact]
    public void Listing_a_missing_folder_returns_nothing()
    {
        Assert.Empty(Files.ListFiles(At("missing")));
    }

    [Fact]
    public void Listing_a_folder_that_holds_only_subfolders_returns_nothing()
    {
        Files.WriteFile(At("tasks", "nested", "a.json"), Bytes("a"));

        Assert.Empty(Files.ListFiles(At("tasks")));
    }

    [Fact]
    public void Moving_a_file_takes_it_from_the_source_to_the_target()
    {
        Files.WriteFile(At("a.json"), Bytes("a"));

        Files.MoveFile(At("a.json"), At("b.json"));

        Assert.False(Files.FileExists(At("a.json")));
        Assert.Equal(Bytes("a"), Files.ReadFile(At("b.json")));
    }

    [Fact]
    public void Moving_a_file_creates_the_target_folders()
    {
        Files.WriteFile(At("a.json"), Bytes("a"));

        Files.MoveFile(At("a.json"), At("new", "deeper", "a.json"));

        Assert.Equal(Bytes("a"), Files.ReadFile(At("new", "deeper", "a.json")));
    }

    [Fact]
    public void Moving_onto_an_existing_file_throws_IOException_and_changes_neither_file()
    {
        Files.WriteFile(At("a.json"), Bytes("a"));
        Files.WriteFile(At("b.json"), Bytes("b"));

        Assert.Throws<IOException>(() => Files.MoveFile(At("a.json"), At("b.json")));

        Assert.Equal(Bytes("a"), Files.ReadFile(At("a.json")));
        Assert.Equal(Bytes("b"), Files.ReadFile(At("b.json")));
    }

    [Fact]
    public void Moving_a_missing_file_throws_FileNotFoundException_and_creates_no_target()
    {
        Assert.Throws<FileNotFoundException>(() => Files.MoveFile(At("missing.json"), At("b.json")));

        Assert.False(Files.FileExists(At("b.json")));
    }

    [Fact]
    public void Deleting_a_file_removes_only_that_file()
    {
        Files.WriteFile(At("a.json"), Bytes("a"));
        Files.WriteFile(At("b.json"), Bytes("b"));

        Files.DeleteFile(At("a.json"));

        Assert.False(Files.FileExists(At("a.json")));
        Assert.True(Files.FileExists(At("b.json")));
    }

    [Fact]
    public void Deleting_a_missing_file_does_nothing()
    {
        Files.WriteFile(At("b.json"), Bytes("b"));

        Files.DeleteFile(At("missing.json"));
        Files.DeleteFile(At("missing-folder", "missing.json"));

        Assert.Equal([At("b.json")], Listed(Root));
    }

    [Fact]
    public void Deleting_a_folder_removes_everything_in_it()
    {
        Files.WriteFile(At("images", "a.png"), Bytes("a"));
        Files.WriteFile(At("images", "nested", "b.png"), Bytes("b"));

        Files.DeleteFolder(At("images"));

        Assert.False(Files.FileExists(At("images", "a.png")));
        Assert.False(Files.FileExists(At("images", "nested", "b.png")));
        Assert.Empty(Files.ListFiles(At("images")));
    }

    [Fact]
    public void Deleting_a_folder_keeps_the_files_beside_it_and_in_folders_with_a_similar_name()
    {
        Files.WriteFile(At("images", "a.png"), Bytes("a"));
        Files.WriteFile(At("images2", "b.png"), Bytes("b"));
        Files.WriteFile(At("c.json"), Bytes("c"));

        Files.DeleteFolder(At("images"));

        Assert.True(Files.FileExists(At("images2", "b.png")));
        Assert.True(Files.FileExists(At("c.json")));
    }

    [Fact]
    public void Deleting_a_missing_folder_does_nothing()
    {
        Files.WriteFile(At("a.json"), Bytes("a"));

        Files.DeleteFolder(At("missing"));

        Assert.True(Files.FileExists(At("a.json")));
    }
}
