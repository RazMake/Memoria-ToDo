namespace Memoria.ToDo.Shared.Sync;

/// <summary>One remote command on an SSH connection, as SshCommandIo uses it.</summary>
internal interface IRemoteCommand : IDisposable
{
    string CommandText { get; }

    /// <summary>Starts the command; the task completes when the command has ended, or faults/cancels once the command is disposed first.</summary>
    Task ExecuteAsync();

    /// <summary>
    /// The stream that writes the command's standard input; disposing it sends EOF. Throws
    /// <see cref="InvalidOperationException"/> once the command has ended, as SSH.NET's does.
    /// </summary>
    Stream CreateInputStream();

    Stream OutputStream { get; }

    /// <summary>The exit status the host reported, or null when it reported none.</summary>
    int? ExitStatus { get; }

    /// <summary>The exit signal the host reported, or null/empty when none.</summary>
    string? ExitSignal { get; }

    /// <summary>What the command wrote to its error output.</summary>
    string Error { get; }
}
