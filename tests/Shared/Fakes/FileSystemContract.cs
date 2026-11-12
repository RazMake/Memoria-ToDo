using System.Text;
using Memoria.ToDo.Shared.Platform;
using Xunit;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// What every <see cref="IFileSystem"/> promises, written once. A test project derives one class from it, marks that
/// class's kind, and supplies the implementation and a folder of its own to work in. The checks say nothing about
/// case-sensitivity, which differs between Windows and Android.
/// </summary>
public abstract class FileSystemContract
{
    /// <summary>The implementation under test, new for each test.</summary>
    protected abstract IFileSystem FileSystem { get; }

    /// <summary>A full path of a folder that's empty or missing and that no other test uses.</summary>
    protected abstract string Root { get; }

    private string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private void Write(string path, string text) => FileSystem.WriteFile(path, Bytes(text));

    private string Read(string path) => Encoding.UTF8.GetString(FileSystem.ReadFile(path));

    private IEnumerable<string> Listed(string folder) => FileSystem.ListFiles(folder).Order();

    [Fact]
    public void Says_a_written_file_exists()
    {
        Write(Path("a.txt"), "x");

        Assert.True(FileSystem.FileExists(Path("a.txt")));
    }

    [Fact]
    public void Says_a_missing_file_does_not_exist()
    {
        Assert.False(FileSystem.FileExists(Path("missing.txt")));
    }

    [Fact]
    public void Says_a_folder_is_not_a_file()
    {
        Write(Path("folder", "a.txt"), "x");

        Assert.False(FileSystem.FileExists(Path("folder")));
    }

    [Fact]
    public void Reads_back_the_whole_content_written()
    {
        byte[] content = [0, 1, 2, 255, 254, 10, 13];
        FileSystem.WriteFile(Path("a.bin"), content);

        Assert.Equal(content, FileSystem.ReadFile(Path("a.bin")));
    }

    [Fact]
    public void Reads_an_empty_file_as_no_bytes()
    {
        FileSystem.WriteFile(Path("empty.txt"), []);

        Assert.Empty(FileSystem.ReadFile(Path("empty.txt")));
    }

    [Fact]
    public void Fails_to_read_a_file_in_a_missing_folder_with_file_not_found()
    {
        Assert.Throws<FileNotFoundException>(() => FileSystem.ReadFile(Path("missing", "deeper", "a.txt")));
    }

    [Fact]
    public void Lists_no_files_for_a_folder_inside_a_missing_folder()
    {
        Assert.Empty(FileSystem.ListFiles(Path("missing", "deeper")));
    }

    [Fact]
    public void Does_nothing_when_deleting_a_file_in_a_missing_folder()
    {
        FileSystem.DeleteFile(Path("missing", "a.txt"));

        Assert.False(FileSystem.FileExists(Path("missing", "a.txt")));
    }

    [Fact]
    public void Does_nothing_when_deleting_a_folder_inside_a_missing_folder()
    {
        FileSystem.DeleteFolder(Path("missing", "deeper"));

        Assert.False(FileSystem.FileExists(Path("missing", "deeper", "a.txt")));
    }

    [Fact]
    public void Fails_to_move_a_source_in_a_missing_folder_with_file_not_found()
    {
        Assert.Throws<FileNotFoundException>(() => FileSystem.MoveFile(Path("missing", "a.txt"), Path("b.txt")));
    }

    [Fact]
    public void Fails_to_read_a_missing_file_with_file_not_found()
    {
        Assert.Throws<FileNotFoundException>(() => FileSystem.ReadFile(Path("missing.txt")));
    }

    [Fact]
    public void Creates_the_folders_of_a_new_file()
    {
        Write(Path("one", "two", "a.txt"), "x");

        Assert.Equal("x", Read(Path("one", "two", "a.txt")));
    }

    [Fact]
    public void Replaces_the_whole_file_with_shorter_content()
    {
        Write(Path("a.txt"), "a long first version");
        Write(Path("a.txt"), "short");

        Assert.Equal("short", Read(Path("a.txt")));
    }

    [Fact]
    public void Leaves_the_old_file_whole_when_a_write_fails()
    {
        Write(Path("a.txt"), "old");

        Assert.ThrowsAny<IOException>(() => Write(Path("a.txt", "inside.txt"), "new"));

        Assert.Equal("old", Read(Path("a.txt")));
    }

    [Fact]
    public void Leaves_no_other_file_behind_when_a_write_fails()
    {
        Write(Path("a.txt"), "old");

        Assert.ThrowsAny<IOException>(() => Write(Path("a.txt", "inside.txt"), "new"));

        Assert.Equal([Path("a.txt")], Listed(Root));
    }

    [Fact]
    public void Lists_the_full_paths_of_the_files_directly_in_a_folder()
    {
        Write(Path("a.txt"), "x");
        Write(Path("b.txt"), "x");

        Assert.Equal([Path("a.txt"), Path("b.txt")], Listed(Root));
    }

    [Fact]
    public void Does_not_list_subfolders_or_the_files_in_them()
    {
        Write(Path("a.txt"), "x");
        Write(Path("sub", "b.txt"), "x");

        Assert.Equal([Path("a.txt")], Listed(Root));
    }

    [Fact]
    public void Lists_no_files_for_a_missing_folder()
    {
        Assert.Empty(FileSystem.ListFiles(Path("missing")));
    }

    [Fact]
    public void Lists_no_files_for_a_folder_with_only_subfolders()
    {
        Write(Path("sub", "b.txt"), "x");

        Assert.Empty(FileSystem.ListFiles(Root));
    }

    [Fact]
    public void Moves_a_file_so_it_is_only_at_the_target()
    {
        Write(Path("a.txt"), "content");

        FileSystem.MoveFile(Path("a.txt"), Path("b.txt"));

        Assert.False(FileSystem.FileExists(Path("a.txt")));
        Assert.Equal("content", Read(Path("b.txt")));
    }

    [Fact]
    public void Creates_the_target_folders_when_moving()
    {
        Write(Path("a.txt"), "content");

        FileSystem.MoveFile(Path("a.txt"), Path("new", "deeper", "b.txt"));

        Assert.Equal("content", Read(Path("new", "deeper", "b.txt")));
    }

    [Fact]
    public void Fails_to_move_onto_an_existing_target_and_leaves_both_files()
    {
        Write(Path("a.txt"), "source");
        Write(Path("b.txt"), "target");

        Assert.ThrowsAny<IOException>(() => FileSystem.MoveFile(Path("a.txt"), Path("b.txt")));

        Assert.Equal("source", Read(Path("a.txt")));
        Assert.Equal("target", Read(Path("b.txt")));
    }

    [Fact]
    public void Fails_to_move_a_missing_source()
    {
        Assert.ThrowsAny<IOException>(() => FileSystem.MoveFile(Path("missing.txt"), Path("b.txt")));

        Assert.False(FileSystem.FileExists(Path("b.txt")));
    }

    [Fact]
    public void Deletes_a_file()
    {
        Write(Path("a.txt"), "x");

        FileSystem.DeleteFile(Path("a.txt"));

        Assert.False(FileSystem.FileExists(Path("a.txt")));
    }

    [Fact]
    public void Deletes_only_the_file_named()
    {
        Write(Path("a.txt"), "x");
        Write(Path("b.txt"), "y");

        FileSystem.DeleteFile(Path("a.txt"));

        Assert.Equal("y", Read(Path("b.txt")));
    }

    [Fact]
    public void Does_nothing_when_deleting_a_missing_file()
    {
        Write(Path("b.txt"), "y");

        FileSystem.DeleteFile(Path("a.txt"));

        Assert.Equal("y", Read(Path("b.txt")));
    }

    [Fact]
    public void Deletes_a_folder_and_everything_in_it()
    {
        Write(Path("folder", "a.txt"), "x");
        Write(Path("folder", "deeper", "b.txt"), "x");

        FileSystem.DeleteFolder(Path("folder"));

        Assert.False(FileSystem.FileExists(Path("folder", "a.txt")));
        Assert.False(FileSystem.FileExists(Path("folder", "deeper", "b.txt")));
    }

    [Fact]
    public void Keeps_the_files_outside_a_deleted_folder()
    {
        Write(Path("folder", "a.txt"), "x");
        Write(Path("folder2", "c.txt"), "keep");
        Write(Path("d.txt"), "keep");

        FileSystem.DeleteFolder(Path("folder"));

        Assert.Equal("keep", Read(Path("folder2", "c.txt")));
        Assert.Equal("keep", Read(Path("d.txt")));
    }

    [Fact]
    public void Does_nothing_when_deleting_a_missing_folder()
    {
        Write(Path("d.txt"), "keep");

        FileSystem.DeleteFolder(Path("missing"));

        Assert.Equal("keep", Read(Path("d.txt")));
    }

    [Fact]
    public void Fails_to_list_a_file_as_if_it_were_a_folder()
    {
        Write(Path("a.txt"), "x");

        Assert.ThrowsAny<IOException>(() => FileSystem.ListFiles(Path("a.txt")));
    }

    [Fact]
    public void Fails_to_delete_a_folder_as_if_it_were_a_file_and_keeps_its_files()
    {
        Write(Path("folder", "a.txt"), "x");

        Assert.ThrowsAny<Exception>(() => FileSystem.DeleteFile(Path("folder")));

        Assert.Equal("x", Read(Path("folder", "a.txt")));
    }

    [Theory]
    [InlineData("folder")]
    [InlineData(@"..\outside")]
    [InlineData("C:folder")]
    [InlineData("C:")]
    public void Rejects_a_relative_or_drive_relative_directory_when_asking_about_containment(string relative)
    {
        Assert.ThrowsAny<ArgumentException>(() => FileSystem.IsSameOrDescendantDirectory(relative, Root));
    }

    [Theory]
    [InlineData("folder")]
    [InlineData(@"..\outside")]
    [InlineData("C:folder")]
    [InlineData("C:")]
    public void Rejects_a_relative_or_drive_relative_ancestor_when_asking_about_containment(string relative)
    {
        Assert.ThrowsAny<ArgumentException>(() => FileSystem.IsSameOrDescendantDirectory(Root, relative));
    }

    [Fact]
    public void Fails_to_delete_a_file_as_if_it_were_a_folder_and_keeps_the_file()
    {
        Write(Path("a.txt"), "x");

        Assert.ThrowsAny<Exception>(() => FileSystem.DeleteFolder(Path("a.txt")));

        Assert.Equal("x", Read(Path("a.txt")));
    }
}
