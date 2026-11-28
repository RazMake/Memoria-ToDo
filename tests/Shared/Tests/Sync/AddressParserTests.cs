using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>The two supported SSH address forms, how each is read, and which texts are refused.</summary>
[Unit]
public sealed class AddressParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://git.example.org/me/tasks.git")]
    [InlineData("ssh://git@git.example.org")]
    [InlineData("ssh://git@git.example.org:0/me/tasks.git")]
    [InlineData("ssh://git@git.example.org:70000/me/tasks.git")]
    [InlineData("git@git.example.org")]
    [InlineData("C:\\tasks")]
    [InlineData("C:/tasks")]
    [InlineData("d:\\work\\tasks.git")]
    public void Rejects_an_unsupported_address(string text)
    {
        Assert.ThrowsAny<ArgumentException>(() => AddressParser.Parse(text));
    }

    [Theory]
    [InlineData("git@git.example.org:me/tasks.git", "git@git.example.org:me/tasks.git", "git", "git.example.org", 22, "me/tasks.git")]
    [InlineData("git.example.org:me/tasks.git", "git.example.org:me/tasks.git", "git", "git.example.org", 22, "me/tasks.git")]
    [InlineData("Me@Git.Example.ORG.:Me/Tasks.GIT", "Me@Git.Example.ORG.:Me/Tasks.GIT", "Me", "git.example.org", 22, "Me/Tasks.GIT")]
    [InlineData("ssh://git@git.example.org/me/tasks.git", "ssh://git@git.example.org/me/tasks.git", "git", "git.example.org", 22, "/me/tasks.git")]
    [InlineData("ssh://deploy@git.example.org:2222/me/tasks.git", "ssh://deploy@git.example.org:2222/me/tasks.git", "deploy", "git.example.org", 2222, "/me/tasks.git")]
    [InlineData("  git@git.example.org:me/tasks.git  ", "git@git.example.org:me/tasks.git", "git", "git.example.org", 22, "me/tasks.git")]
    public void Reads_the_user_host_port_and_path_of_an_address(string text, string canonical, string user, string host, int port, string path)
    {
        var address = AddressParser.Parse(text);

        Assert.Equal(canonical, address.Text);
        Assert.Equal(user, AddressParser.User(address.Text));
        Assert.Equal(host, address.Host);
        Assert.Equal(port, address.Port);
        Assert.Equal(path, address.Path);
    }

    // A bare one-letter host is written with its SSH user, so Git never reads `g:me/tasks.git` as a local path.
    [Theory]
    [InlineData("g:me/tasks.git", "git@g:me/tasks.git")]
    [InlineData("G:dir/sub/repo.git", "git@G:dir/sub/repo.git")]
    [InlineData("  g:me/tasks.git  ", "git@g:me/tasks.git")]
    [InlineData("git@g:me/tasks.git", "git@g:me/tasks.git")]
    [InlineData("deploy@g:repo.git", "deploy@g:repo.git")]
    [InlineData("gh:me/tasks.git", "gh:me/tasks.git")]
    [InlineData("ssh://g/me/tasks.git", "ssh://g/me/tasks.git")]
    public void Gives_a_bare_single_letter_scp_host_its_ssh_user_and_keeps_every_other_address_as_written(string text, string canonical)
    {
        Assert.Equal(canonical, AddressParser.Parse(text).Text);
    }

    [Theory]
    [InlineData("git@github.com:me/tasks.git", "https://github.com/me/tasks/settings/keys/new")]
    [InlineData("git@gitlab.com:group/sub/tasks.git", "https://gitlab.com/group/sub/tasks/-/deploy_keys")]
    [InlineData("git@bitbucket.org:me/tasks.git", "https://bitbucket.org/account/settings/ssh-keys/")]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/tasks", "https://dev.azure.com/org/_usersSettings/keys")]
    public void Links_the_page_where_a_well_known_host_takes_a_deploy_key(string origin, string page)
    {
        Assert.Equal(page, AddressParser.AddKeyPage(origin)?.AbsoluteUri);
    }

    [Theory]
    [InlineData("git@git.example.org:me/tasks.git")]
    [InlineData("not an address")]
    public void Links_no_page_for_an_unknown_host_or_an_unsupported_address(string origin)
    {
        Assert.Null(AddressParser.AddKeyPage(origin));
    }
}
