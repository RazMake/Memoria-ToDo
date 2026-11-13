using Renci.SshNet;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>A connected SSH.NET client as an <see cref="IRemoteHost"/>.</summary>
internal sealed class SshNetRemoteHost(SshClient client) : IRemoteHost
{
    public IRemoteCommand CreateCommand(string commandText) => new SshNetRemoteCommand(client.CreateCommand(commandText));

    public void Disconnect() => client.Disconnect();

    public void Dispose() => client.Dispose();
}
