using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>How the outcome of checking a host's key becomes the failure a sync reports.</summary>
[Unit]
public sealed class HostKeyCheckTests
{
    private const string Host = "git.example.org";

    [Fact]
    public void Trusts_the_connection_and_reports_no_failure_for_a_trusted_key()
    {
        var outcome = HostKeyCheck.Run(Host, () => HostKeyVerdict.Trusted);

        Assert.True(outcome.CanTrust);
        Assert.Null(outcome.Failure);
    }

    [Fact]
    public void Refuses_the_connection_with_an_unknown_host_key_failure_for_a_key_that_is_not_trusted_yet()
    {
        var outcome = HostKeyCheck.Run(Host, () => HostKeyVerdict.Unknown);

        Assert.False(outcome.CanTrust);
        Assert.Equal(SyncFailure.UnknownHostKey, outcome.Failure!.Kind);
        Assert.Contains(Host, outcome.Failure.Message);
    }

    [Fact]
    public void Refuses_the_connection_with_a_changed_host_key_failure_for_a_key_that_differs_from_the_trusted_one()
    {
        var outcome = HostKeyCheck.Run(Host, () => HostKeyVerdict.Changed);

        Assert.False(outcome.CanTrust);
        Assert.Equal(SyncFailure.ChangedHostKey, outcome.Failure!.Kind);
        Assert.Contains(Host, outcome.Failure.Message);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(System.Security.Cryptography.CryptographicException))]
    public void Refuses_the_connection_with_a_local_storage_failure_keeping_the_cause_when_the_saved_trust_cannot_be_loaded(Type type)
    {
        var cause = (Exception)Activator.CreateInstance(type, "The trust file is unusable.")!;

        var outcome = HostKeyCheck.Run(Host, () => throw cause);

        Assert.False(outcome.CanTrust);
        Assert.Equal(SyncFailure.LocalStorage, outcome.Failure!.Kind);
        Assert.Equal("The trust file is unusable.", outcome.Failure.Message);
        Assert.Same(cause, outcome.Failure.InnerException);
    }

    [Theory]
    [InlineData(typeof(ObjectDisposedException))]
    [InlineData(typeof(NotSupportedException))]
    public void Refuses_the_connection_with_an_unexpected_failure_keeping_the_cause_for_a_failure_that_is_not_about_this_devices_storage(Type type)
    {
        var cause = (Exception)Activator.CreateInstance(type, "The trust check broke.")!;

        var outcome = HostKeyCheck.Run(Host, () => throw cause);

        Assert.False(outcome.CanTrust);
        Assert.Equal(SyncFailure.Unexpected, outcome.Failure!.Kind);
        Assert.Equal(cause.Message, outcome.Failure.Message);
        Assert.Same(cause, outcome.Failure.InnerException);
    }

    [Fact]
    public void Refuses_the_connection_with_an_unexpected_failure_keeping_the_cause_for_a_cancellation_nobody_requested()
    {
        var cause = new OperationCanceledException("The check gave up.");

        var outcome = HostKeyCheck.Run(Host, () => throw cause);

        Assert.False(outcome.CanTrust);
        Assert.Equal(SyncFailure.Unexpected, outcome.Failure!.Kind);
        Assert.Equal(cause.Message, outcome.Failure.Message);
        Assert.Same(cause, outcome.Failure.InnerException);
    }
}
