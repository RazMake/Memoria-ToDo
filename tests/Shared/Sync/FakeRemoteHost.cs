namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>A host that hands out the one command a test drives.</summary>
internal sealed class FakeRemoteHost(FakeRemoteCommand command) : IRemoteHost
{
    /// <summary>When set, closing the connection (disposing the host) fails with it.</summary>
    public Exception? DisposeFailure { get; set; }

    public IRemoteCommand CreateCommand(string commandText) => command;

    public void Disconnect()
    {
    }

    public void Dispose()
    {
        if (DisposeFailure is { } failure)
        {
            throw failure;
        }
    }
}
