using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>How the composition root tells a start at sign-in from a start by hand, through <c>StartArguments</c>.</summary>
[Unit]
public class StartArgumentsTests
{
    [Fact]
    public void The_sign_in_argument_means_a_start_at_sign_in()
    {
        Assert.True(StartArguments.IsStartAtSignIn(["--at-sign-in"]));
    }

    [Fact]
    public void The_sign_in_argument_among_others_means_a_start_at_sign_in()
    {
        Assert.True(StartArguments.IsStartAtSignIn(["--other", "--at-sign-in"]));
    }

    [Fact]
    public void No_argument_means_a_start_by_hand()
    {
        Assert.False(StartArguments.IsStartAtSignIn([]));
    }

    [Fact]
    public void Other_arguments_mean_a_start_by_hand()
    {
        Assert.False(StartArguments.IsStartAtSignIn(["--other", "at-sign-in"]));
    }

    [Fact]
    public void No_argument_list_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => StartArguments.IsStartAtSignIn(null!));
    }

    [Fact]
    public void The_mcp_argument_means_the_program_is_the_bridge()
    {
        Assert.True(StartArguments.IsMcpBridge(["--mcp"]));
    }

    [Fact]
    public void The_mcp_argument_among_others_means_the_program_is_the_bridge()
    {
        Assert.True(StartArguments.IsMcpBridge(["--other", "--mcp"]));
    }

    [Fact]
    public void No_argument_means_the_program_is_not_the_bridge()
    {
        Assert.False(StartArguments.IsMcpBridge([]));
    }

    [Fact]
    public void The_sign_in_argument_and_look_alikes_do_not_mean_the_bridge()
    {
        Assert.False(StartArguments.IsMcpBridge(["--at-sign-in", "mcp", "--mcp-server"]));
    }

    [Fact]
    public void No_argument_list_is_refused_when_asking_for_the_bridge()
    {
        Assert.Throws<ArgumentNullException>(() => StartArguments.IsMcpBridge(null!));
    }
}
