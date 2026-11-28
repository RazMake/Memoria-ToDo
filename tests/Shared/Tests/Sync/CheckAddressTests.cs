using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// Check address: Configure asks Sync whether Clone and Use existing accept a typed address, and which add-key page
/// belongs to its host. The answer comes from the one address parser, with no I/O, whatever key or clone the device has.
/// </summary>
[Unit]
public sealed class CheckAddressTests
{
    private readonly SyncFixture f = new();

    [Theory]
    [InlineData("git@github.com:me/tasks.git", "https://github.com/me/tasks/settings/keys/new")]
    [InlineData("ssh://git@github.com/me/tasks.git", "https://github.com/me/tasks/settings/keys/new")]
    [InlineData("git@gitlab.com:group/sub/tasks.git", "https://gitlab.com/group/sub/tasks/-/deploy_keys")]
    [InlineData("git@bitbucket.org:me/tasks.git", "https://bitbucket.org/account/settings/ssh-keys/")]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/tasks", "https://dev.azure.com/org/_usersSettings/keys")]
    public async Task Accepts_the_address_of_a_well_known_host_and_gives_its_add_key_page(string address, string page)
    {
        await using var sync = f.Create();

        var accepted = sync.Setup.CheckAddress(address, out var addKeyPage);

        Assert.True(accepted);
        Assert.Equal(page, addKeyPage?.AbsoluteUri);
    }

    [Theory]
    [InlineData("git@git.example.org:me/tasks.git")]
    [InlineData("ssh://deploy@git.example.org:2222/me/tasks.git")]
    [InlineData("g:me/tasks.git")]
    [InlineData("git@gitlab.com:tasks.git")]
    [InlineData("git@ssh.dev.azure.com:v3/org/tasks")]
    public async Task Accepts_a_custom_host_or_an_address_the_parser_gives_no_page_with_no_page(string address)
    {
        await using var sync = f.Create();

        var accepted = sync.Setup.CheckAddress(address, out var addKeyPage);

        Assert.True(accepted);
        Assert.Null(addKeyPage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://github.com/me/tasks.git")]
    [InlineData("ssh://git@git.example.org")]
    [InlineData("ssh://git@git.example.org:0/me/tasks.git")]
    [InlineData("ssh://git@git.example.org:70000/me/tasks.git")]
    [InlineData("git@git.example.org")]
    [InlineData("C:\\tasks")]
    [InlineData("C:/tasks")]
    [InlineData("not an address")]
    public async Task Refuses_an_address_the_parser_rejects_and_gives_no_page(string address)
    {
        await using var sync = f.Create();

        var accepted = sync.Setup.CheckAddress(address, out var addKeyPage);

        Assert.False(accepted);
        Assert.Null(addKeyPage);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.CloneAsync(address, f.CloneFolder, CancellationToken.None));
    }

    [Fact]
    public async Task Reads_an_address_with_surrounding_spaces_as_the_parser_does()
    {
        await using var sync = f.Create();

        var accepted = sync.Setup.CheckAddress("  git@github.com:me/tasks.git  ", out var addKeyPage);

        Assert.True(accepted);
        Assert.Equal("https://github.com/me/tasks/settings/keys/new", addKeyPage?.AbsoluteUri);
    }

    [Fact]
    public async Task Answers_before_the_service_starts_without_a_key_or_a_clone_and_does_no_file_or_git_work()
    {
        await using var sync = f.Create();

        sync.Setup.CheckAddress("git@github.com:me/tasks.git", out _);
        sync.Setup.CheckAddress("not an address", out _);

        Assert.Empty(f.Files.AllFilesUnder(f.DeviceFolder));
        Assert.Equal(0, f.Git.ConnectionsOpened);
        Assert.Equal(0, f.Git.OriginReads);
        Assert.Equal(0, f.Keys.OpenCalls);
        Assert.Equal(0, f.Keys.GenerateCalls);
        Assert.Equal(0, f.Files.PhysicalChecks);
    }

    [Fact]
    public async Task Answers_at_once_while_a_pass_runs_and_changes_nothing()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        (bool accepted, Uri? page) answer;
        try
        {
            answer = await Task.Run(() =>
            {
                var accepted = sync.Setup.CheckAddress("git@github.com:me/tasks.git", out var page);
                return (accepted, page);
            }).WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        }
        finally
        {
            held.Complete();
        }

        await running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.True(answer.accepted);
        Assert.NotNull(answer.page);
        Assert.Equal(f.CloneFolder, f.Tasks.RepositoryFolder);
        Assert.Empty(f.Git.Cloned);
        Assert.Empty(f.Git.UsedExisting);
    }
}
