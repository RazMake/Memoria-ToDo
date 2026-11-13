using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// The failure a native call reports is the most specific one the transport recorded: a plain I/O error closing the
/// channel is only a connection failure until Sync names the real cause. Tests stay synchronous because the scope is per thread.
/// </summary>
[Unit]
public sealed class OperationScopeTests
{
    private readonly FakeDeviceKeyStore keys = new();

    private OperationScope Enter(out IDeviceSigningKey key)
    {
        key = keys.Generate();
        return OperationScope.Enter(
            new GitConnection(key, "Laptop", (_, _, _, _) => HostKeyVerdict.Trusted, new TransportCleanup()),
            CancellationToken.None,
            TimeProvider.System);
    }

    [Theory]
    [InlineData(SyncFailure.Repository)]
    [InlineData(SyncFailure.Authentication)]
    public void Reports_the_authoritative_failure_when_it_follows_a_plain_io_error_from_the_closed_channel(SyncFailure authoritative)
    {
        using var scope = Enter(out var key);
        using var _ = key;

        scope.Record(new IOException("The channel was closed."));
        scope.Record(new GitFailureException(authoritative, "The server rejected the request."));

        Assert.Equal(authoritative, scope.Failure!.Kind);
    }

    [Fact]
    public void Keeps_the_first_plain_io_error_when_another_plain_io_error_follows()
    {
        using var scope = Enter(out var key);
        using var _ = key;

        scope.Record(new IOException("The first fault."));
        scope.Record(new IOException("The second fault."));

        Assert.Equal(SyncFailure.Connection, scope.Failure!.Kind);
        Assert.Equal("The first fault.", scope.Failure.Message);
    }

    [Fact]
    public void Keeps_the_first_plain_io_error_when_a_connection_failure_that_Sync_classified_follows()
    {
        using var scope = Enter(out var key);
        using var _ = key;

        scope.Record(new IOException("The first fault."));
        scope.Record(new GitFailureException(SyncFailure.Connection, "The host sent no data."));

        Assert.Equal("The first fault.", scope.Failure!.Message);
    }

    [Fact]
    public void Keeps_a_repository_failure_when_an_authentication_failure_follows()
    {
        using var scope = Enter(out var key);
        using var _ = key;

        scope.Record(new GitFailureException(SyncFailure.Repository, "The server rejected the request."));
        scope.Record(new GitFailureException(SyncFailure.Authentication, "The key was refused."));

        Assert.Equal(SyncFailure.Repository, scope.Failure!.Kind);
    }

    [Fact]
    public void Keeps_a_connection_failure_that_Sync_reported_even_when_a_repository_failure_follows()
    {
        using var scope = Enter(out var key);
        using var _ = key;

        scope.Record(new GitFailureException(SyncFailure.Connection, "The host sent no data."));
        scope.Record(new GitFailureException(SyncFailure.Repository, "The server rejected the request."));

        Assert.Equal(SyncFailure.Connection, scope.Failure!.Kind);
    }
}
