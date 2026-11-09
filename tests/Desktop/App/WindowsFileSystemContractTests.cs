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

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
