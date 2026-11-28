using Renci.SshNet;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>An SSH.NET command as an <see cref="IRemoteCommand"/>.</summary>
internal sealed class SshNetRemoteCommand(SshCommand command) : IRemoteCommand
{
    public string CommandText => command.CommandText;

    public Task ExecuteAsync() => command.ExecuteAsync(CancellationToken.None);

    public Stream CreateInputStream() => command.CreateInputStream();

    public Stream OutputStream => command.OutputStream;

    public int? ExitStatus => command.ExitStatus;

    public string? ExitSignal => command.ExitSignal;

    public string Error => command.Error;

    public void Dispose() => command.Dispose();
}
