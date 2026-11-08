using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>How the composition root tells a start at sign-in from a start by hand, through <c>StartArguments</c>.</summary>
[Unit]
public class StartArgumentsTests
{
    [Fact]
    public void The_sign_in_argument_is_at_sign_in()
    {
        Assert.Equal("--at-sign-in", StartArguments.AtSignIn);
    }

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
}
