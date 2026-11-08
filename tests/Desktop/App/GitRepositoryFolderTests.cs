using System.IO;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The git repository folder adapter, on a scratch folder of its own for each test. Clone runs the installed git.</summary>
[Integration]
public sealed class GitRepositoryFolderTests : IDisposable
{
    private readonly string scratch = Directory.CreateTempSubdirectory("memoria-repo-tests-").FullName;

    private string SettingFile => Path.Combine(scratch, "setting", "repository-folder.txt");

    [Fact]
    public void Has_no_folder_before_one_is_set()
    {
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);

        Assert.Null(folder.Current());
    }

    [Fact]
    public void Use_existing_keeps_a_folder_with_a_git_folder_for_the_next_start()
    {
        var clone = Directory.CreateDirectory(Path.Combine(scratch, "clone")).FullName;
        Directory.CreateDirectory(Path.Combine(clone, ".git"));

        new GitRepositoryFolder(SettingFile).UseExisting(clone);

        Assert.Equal(clone, new GitRepositoryFolder(SettingFile).Current());
    }

    [Fact]
    public void Use_existing_refuses_a_folder_that_is_not_a_clone_and_keeps_none()
    {
        var plain = Directory.CreateDirectory(Path.Combine(scratch, "plain")).FullName;
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);

        var error = Assert.Throws<InvalidOperationException>(() => folder.UseExisting(plain));

        Assert.Contains("isn't a git clone", error.Message);
        Assert.Null(folder.Current());
    }

    [Fact]
    public async Task Clone_clones_a_local_repository_and_makes_it_current()
    {
        var source = Directory.CreateDirectory(Path.Combine(scratch, "source")).FullName;
        await Git(source, "init");
        var target = Path.Combine(scratch, "target");
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);

        await folder.CloneAsync(source, target, TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
        Assert.Equal(target, folder.Current());
    }

    [Fact]
    public async Task Remote_is_the_address_the_clone_was_made_from()
    {
        var source = Directory.CreateDirectory(Path.Combine(scratch, "source")).FullName;
        await Git(source, "init");
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);
        await folder.CloneAsync(source, Path.Combine(scratch, "target"), TestContext.Current.CancellationToken);

        Assert.Equal(source, await folder.RemoteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remote_is_null_when_there_is_no_folder()
    {
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);

        Assert.Null(await folder.RemoteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remote_is_null_for_a_clone_without_an_origin()
    {
        var clone = Directory.CreateDirectory(Path.Combine(scratch, "clone")).FullName;
        await Git(clone, "init");
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);
        folder.UseExisting(clone);

        Assert.Null(await folder.RemoteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Identity_is_the_name_and_email_the_clone_commits_as()
    {
        var clone = Directory.CreateDirectory(Path.Combine(scratch, "clone")).FullName;
        await Git(clone, "init");
        await Git(clone, "config", "user.name", "Ana Pop");
        await Git(clone, "config", "user.email", "ana@example.com");
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);
        folder.UseExisting(clone);

        Assert.Equal("Ana Pop <ana@example.com>", await folder.IdentityAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Identity_is_null_when_there_is_no_folder()
    {
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);

        Assert.Null(await folder.IdentityAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_failed_clone_says_why_and_keeps_no_folder()
    {
        IRepositoryFolder folder = new GitRepositoryFolder(SettingFile);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            folder.CloneAsync(Path.Combine(scratch, "missing"), Path.Combine(scratch, "target"), TestContext.Current.CancellationToken));

        Assert.Contains("couldn't clone", error.Message);
        Assert.Null(folder.Current());
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(scratch, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(scratch, recursive: true);
    }

    private static async Task Git(string folder, params string[] command)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git")
        {
            ArgumentList = { "-C", folder },
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var part in command)
        {
            start.ArgumentList.Add(part);
        }

        using var git = System.Diagnostics.Process.Start(start)!;
        await git.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, git.ExitCode);
    }
}
