using System.Net.Sockets;
using Memoria.ToDo.Testing;
using Renci.SshNet.Common;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>How a failure to connect to a Git host becomes the failure a sync reports.</summary>
[Unit]
public sealed class SshLinkClassifyTests
{
    private static readonly SshAddress Address = new("git@git.example.org:me/tasks.git", "git.example.org", 22, "me/tasks.git");

    public static TheoryData<Exception> NetworkFailures => new()
    {
        new SshConnectionException("The connection was lost."),
        new SocketException((int)SocketError.ConnectionRefused),
        new SshOperationTimeoutException("The host didn't answer."),
    };

    [Theory]
    [MemberData(nameof(NetworkFailures))]
    public void Reports_a_connection_failure_naming_the_host_and_port_for_a_network_or_ssh_connection_failure(Exception cause)
    {
        var failure = SshLink.Classify(Address, cause);

        Assert.Equal(SyncFailure.Connection, failure.Kind);
        Assert.Contains("git.example.org:22", failure.Message);
        Assert.Same(cause, failure.InnerException);
    }

    [Fact]
    public void Reports_an_authentication_failure_when_the_host_refuses_the_key()
    {
        var cause = new SshAuthenticationException("Permission denied.");

        var failure = SshLink.Classify(Address, cause);

        Assert.Equal(SyncFailure.Authentication, failure.Kind);
        Assert.Contains("git.example.org", failure.Message);
        Assert.Same(cause, failure.InnerException);
    }

    [Fact]
    public void Keeps_a_failure_that_is_already_classified_even_when_it_is_wrapped()
    {
        var classified = new GitFailureException(SyncFailure.LocalStorage, "The key can't sign.");

        var failure = SshLink.Classify(Address, new SshConnectionException("Wrapped.", classified));

        Assert.Same(classified, failure);
    }

    [Theory]
    [InlineData(typeof(ObjectDisposedException), SyncFailure.Unexpected)]
    [InlineData(typeof(NotSupportedException), SyncFailure.Unexpected)]
    [InlineData(typeof(InvalidDataException), SyncFailure.LocalStorage)]
    [InlineData(typeof(UnauthorizedAccessException), SyncFailure.LocalStorage)]
    public void Reports_a_failure_that_is_not_about_the_network_through_the_shared_rule_keeping_the_cause(Type type, SyncFailure expected)
    {
        var cause = (Exception)Activator.CreateInstance(type, "It broke.")!;

        var failure = SshLink.Classify(Address, cause);

        Assert.Equal(expected, failure.Kind);
        Assert.Equal(cause.Message, failure.Message);
        Assert.Same(cause, failure.InnerException);
    }
}
