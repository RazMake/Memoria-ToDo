using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Serilog;

namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>
/// Takes connections on a named pipe that only the current user can open, and hands each one's stream to the server.
/// Exactly one pipe instance waits at a time; it is replaced when someone connects. Stops when told to, never throws.
/// </summary>
/// <remarks>
/// <paramref name="serve"/> serves one connection and completes when it ends. <paramref name="ownsInstances"/> says whether this
/// server still serves pipe instances of an earlier listener: the name then is already this user's, so the first instance of this
/// listener can't claim it as the first.
/// </remarks>
internal sealed class PipeListener(string pipeName, Func<Stream, Task> serve, Func<bool> ownsInstances, ILogger logger, Action listening, Action<string> failed)
{
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// Starts listening once <paramref name="previous"/> has ended, at once when it already has, so a pipe that was just closed
    /// can be opened again. The returned task completes when this listener has stopped, and never fails.
    /// </summary>
    public Task Start(Task previous) => ListenAsync(previous);

    /// <summary>Stops waiting for connections. Connections already handed over aren't ended.</summary>
    public void Stop() => _stopping.Cancel();

    private async Task ListenAsync(Task previous)
    {
        if (!previous.IsCompleted)
        {
            await previous.ConfigureAwait(false);
        }

        var token = _stopping.Token;
        var first = true;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var pipe = Create(first);
                if (pipe is null)
                {
                    return;
                }

                if (first)
                {
                    first = false;
                    logger.Information("Listening on the pipe {Pipe}", pipeName);
                    listening();
                }

                try
                {
                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    return;
                }
                catch (IOException e)
                {
                    logger.Debug(e, "A client left the pipe {Pipe} before it was served", pipeName);
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    continue;
                }

                _ = serve(pipe);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.Error(e, "Listening on the pipe {Pipe} failed", pipeName);
            failed($"Listening on the pipe {pipeName} failed: {e.Message}");
        }
        finally
        {
            logger.Information("Stopped listening on the pipe {Pipe}", pipeName);
        }
    }

    // The next instance of the pipe, or null after reporting that it can't be created. The first one must be the first instance of
    // the name, so a pipe another program made is never joined, unless an instance of this server's own still serves.
    private NamedPipeServerStream? Create(bool first)
    {
        var claimsName = first && !ownsInstances();
        try
        {
            return NamedPipeServerStreamAcl.Create(
                pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                claimsName ? PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance : PipeOptions.Asynchronous,
                0, 0, CurrentUserOnly());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.Error(e, "The pipe {Pipe} can't be opened", pipeName);
            failed(first
                ? $"The pipe {pipeName} can't be opened, probably because another copy of the app is using it: {e.Message}"
                : $"The pipe {pipeName} can't be opened again: {e.Message}");
            return null;
        }
    }

    // Allows the current user to connect and nobody else, and keeps connections from the network out.
    private static PipeSecurity CurrentUserOnly()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User!;
        var security = new PipeSecurity();
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }
}
