namespace Memoria.ToDo.Shared.Sync;

/// <summary>A connected SSH host, as SshLink uses it.</summary>
internal interface IRemoteHost : IDisposable
{
    /// <summary>Creates the command that runs the given text on the host; it doesn't start until executed.</summary>
    IRemoteCommand CreateCommand(string commandText);

    /// <summary>Ends the connection, which unblocks any command still waiting on the host.</summary>
    void Disconnect();
}
