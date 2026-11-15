using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>Runs the file system contract on the real Windows file system, in a scratch folder removed afterwards.</summary>
[Integration]
public sealed class WindowsFileSystemContractTests : FileSystemContract, IDisposable
{
    protected override IFileSystem FileSystem { get; } = new WindowsFileSystem();

    protected override string Root { get; } = Path.Combine(Path.GetTempPath(), "windows-file-system", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Fails_to_delete_a_file_another_process_holds_open_and_keeps_it()
    {
        var path = Path.Combine(Root, "locked.txt");
        FileSystem.WriteFile(path, [1]);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.ThrowsAny<IOException>(() => FileSystem.DeleteFile(path));

        Assert.True(FileSystem.FileExists(path));
    }

    [Fact]
    public void Fails_to_delete_a_folder_holding_a_file_that_is_open_and_keeps_the_file()
    {
        var path = Path.Combine(Root, "folder", "locked.txt");
        FileSystem.WriteFile(path, [1]);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.ThrowsAny<IOException>(() => FileSystem.DeleteFolder(Path.Combine(Root, "folder")));

        Assert.True(FileSystem.FileExists(path));
    }

    [Fact]
    public void Fails_to_delete_a_read_only_file_and_keeps_it()
    {
        var path = Path.Combine(Root, "readonly.txt");
        FileSystem.WriteFile(path, [1]);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert.ThrowsAny<Exception>(() => FileSystem.DeleteFile(path));
            Assert.True(FileSystem.FileExists(path));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Fails_to_list_a_folder_it_may_not_read_instead_of_saying_it_is_empty()
    {
        var folder = Path.Combine(Root, "denied");
        FileSystem.WriteFile(Path.Combine(folder, "a.txt"), [1]);
        var info = new DirectoryInfo(folder);
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var deny = new System.Security.AccessControl.FileSystemAccessRule(
            user, System.Security.AccessControl.FileSystemRights.ListDirectory,
            System.Security.AccessControl.AccessControlType.Deny);
        var security = info.GetAccessControl();
        security.AddAccessRule(deny);
        info.SetAccessControl(security);
        try
        {
            Assert.ThrowsAny<UnauthorizedAccessException>(() => FileSystem.ListFiles(folder));
        }
        finally
        {
            security.RemoveAccessRule(deny);
            info.SetAccessControl(security);
        }
    }

    [Fact]
    public void Sees_a_folder_reached_through_a_junction_as_the_folder_it_points_to()
    {
        var target = Directory.CreateDirectory(Path.Combine(Root, "target")).FullName;
        var junction = Junction("link", target);

        Assert.True(FileSystem.IsSameOrDescendantDirectory(junction, target));
        Assert.True(FileSystem.IsSameOrDescendantDirectory(target, junction));
    }

    [Fact]
    public void Sees_a_folder_that_does_not_exist_yet_below_a_junction_as_inside_the_folder_it_points_to()
    {
        var target = Directory.CreateDirectory(Path.Combine(Root, "target")).FullName;
        var junction = Junction("link", target);
        var missing = Path.Combine(junction, "not", "there");

        Assert.True(FileSystem.IsSameOrDescendantDirectory(missing, target));
        Assert.False(Directory.Exists(Path.Combine(target, "not")));
    }

    [Fact]
    public void Does_not_see_a_junction_to_an_unrelated_folder_as_inside_the_ancestor()
    {
        var ancestor = Directory.CreateDirectory(Path.Combine(Root, "ancestor")).FullName;
        var elsewhere = Directory.CreateDirectory(Path.Combine(Root, "elsewhere")).FullName;
        var junction = Junction(Path.Combine("ancestor", "link"), elsewhere);

        Assert.False(FileSystem.IsSameOrDescendantDirectory(junction, ancestor));
    }

    [Fact]
    public void Fails_instead_of_guessing_when_the_path_is_a_file_not_a_folder_and_leaves_the_file_whole()
    {
        var file = Path.Combine(Root, "plain.txt");
        FileSystem.WriteFile(file, [1, 2, 3]);

        Assert.ThrowsAny<IOException>(() => FileSystem.IsSameOrDescendantDirectory(file, Root));

        Assert.Equal([1, 2, 3], FileSystem.ReadFile(file));
    }

    [Fact]
    public void Fails_instead_of_guessing_when_a_junction_points_to_a_folder_that_is_gone()
    {
        var target = Directory.CreateDirectory(Path.Combine(Root, "gone")).FullName;
        var junction = Junction("dangling", target);
        Directory.Delete(target);

        Assert.ThrowsAny<IOException>(() => FileSystem.IsSameOrDescendantDirectory(junction, Root));
    }

    [Fact]
    public void Sees_the_system_drive_root_as_the_same_folder_as_itself()
    {
        var driveRoot = Path.GetPathRoot(Environment.SystemDirectory)!;

        Assert.True(FileSystem.IsSameOrDescendantDirectory(driveRoot, driveRoot));
    }

    [Fact]
    public void Sees_a_scratch_folder_as_inside_its_own_drive_root_and_the_root_as_not_inside_it()
    {
        var scratch = Directory.CreateDirectory(Path.Combine(Root, "below-root")).FullName;
        var driveRoot = Path.GetPathRoot(scratch)!;

        Assert.True(FileSystem.IsSameOrDescendantDirectory(scratch, driveRoot));
        Assert.False(FileSystem.IsSameOrDescendantDirectory(driveRoot, scratch));
    }

    [Fact]
    public void Sees_the_extended_length_spelling_of_a_folder_as_the_same_folder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Root, "plain")).FullName;
        var extended = @"\\?\" + folder;

        Assert.True(FileSystem.IsSameOrDescendantDirectory(extended, folder));
        Assert.True(FileSystem.IsSameOrDescendantDirectory(folder, extended));
    }

    [Fact]
    public void Sees_a_folder_below_an_extended_length_ancestor_and_not_the_reverse()
    {
        var ancestor = Directory.CreateDirectory(Path.Combine(Root, "ancestor")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(ancestor, "child")).FullName;
        var missing = Path.Combine(ancestor, "not", "there");

        Assert.True(FileSystem.IsSameOrDescendantDirectory(child, @"\\?\" + ancestor));
        Assert.True(FileSystem.IsSameOrDescendantDirectory(@"\\?\" + missing, ancestor));
        Assert.False(FileSystem.IsSameOrDescendantDirectory(ancestor, @"\\?\" + child));
    }

    private string Junction(string relativeLink, string target)
    {
        var link = Path.Combine(Root, relativeLink);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        using var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        mklink.StandardOutput.ReadToEnd();
        var error = mklink.StandardError.ReadToEnd();
        mklink.WaitForExit();
        Assert.True(mklink.ExitCode == 0, $"Could not make the junction: {error}");
        return link;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
