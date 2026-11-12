using System.Net.Sockets;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// One SSH connection to a Git host. It connects synchronously on the caller's thread, so signing happens on the
/// worker. Closing never blocks: it returns the task that ends the commands, then the connection.
/// </summary>
internal sealed class SshLink
{
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(30);

    private readonly SshClient client;
    private readonly OperationScope scope;
    private readonly InactivityWatchdog watchdog;
    private readonly List<SshCommandIo> commands = [];

    private SshLink(SshClient client, OperationScope scope)
    {
        this.client = client;
        this.scope = scope;
        // A host that stays silent for the whole timeout is cut off, which ends the blocked read. The timeout is
        // recorded first, so the errors the cut-off causes are only secondary.
        watchdog = new InactivityWatchdog(scope.Time, NetworkTimeout, CutOff);
    }

    private void CutOff()
    {
        scope.Record(new GitFailureException(SyncFailure.Connection, "The host sent no data for the whole network timeout."));
        SshCommandIo[] open;
        lock (commands)
        {
            open = [.. commands];
        }

        foreach (var command in open)
        {
            command.Unblock();
        }

        Disconnect();
    }

    private void Disconnect()
    {
        try
        {
            client.Disconnect();
        }
        catch (Exception ex)
        {
            scope.Record(ex);
        }
    }

    /// <summary>Connects, authenticating with the connection's device key and verifying the host key through Sync.</summary>
    public static SshLink Open(SshAddress address, OperationScope scope)
    {
        var connection = scope.Connection;
        var user = AddressParser.User(address.Text);
        var info = new ConnectionInfo(address.Host, address.Port, user,
            new PrivateKeyAuthenticationMethod(user, new DeviceKeySource(connection.Key)))
        {
            Timeout = NetworkTimeout,
        };
        var client = new SshClient(info);
        GitFailureException? hostKeyFailure = null;
        client.HostKeyReceived += (_, e) =>
        {
            HostKeyVerdict verdict;
            try
            {
                verdict = connection.VerifyHostKey(address.Host, address.Port, e.HostKeyName, DeviceMetadata.Fingerprint(e.HostKey));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // Not reading the saved trust is this device's storage trouble, not the host's; SSH.NET would only wrap it.
                var (kind, details) = FailureReport.OfLocal(ex);
                e.CanTrust = false;
                hostKeyFailure = new GitFailureException(kind, details, ex);
                scope.Record(hostKeyFailure);
                return;
            }

            e.CanTrust = verdict == HostKeyVerdict.Trusted;
            hostKeyFailure = verdict switch
            {
                HostKeyVerdict.Unknown => new GitFailureException(SyncFailure.UnknownHostKey, $"{address.Host} presented a host key that isn't trusted yet."),
                HostKeyVerdict.Changed => new GitFailureException(SyncFailure.ChangedHostKey, $"{address.Host} presented a different host key than the one trusted."),
                _ => null,
            };
        };

        // Connecting is synchronous and each of its stages is bounded by the network timeout, not by the token, so the
        // token is checked on either side of it.
        scope.Token.ThrowIfCancellationRequested();
        try
        {
            client.Connect();
        }
        catch (Exception ex)
        {
            client.Dispose();
            throw hostKeyFailure ?? Classify(address, ex);
        }

        if (scope.Token.IsCancellationRequested)
        {
            client.Dispose();
            scope.Token.ThrowIfCancellationRequested();
        }

        return new SshLink(client, scope);
    }

    public SshCommandIo Start(string service, string path, CancellationToken token)
    {
        var command = client.CreateCommand($"{service} {Quote(path)}");
        // From here the link owns the command, even when starting it fails: CloseAsync ends it and joins its cancel work.
        var io = new SshCommandIo(command, token, Disconnect, watchdog, scope);
        lock (commands)
        {
            commands.Add(io);
        }

        io.Begin();
        return io;
    }

    /// <summary>
    /// Ends every command, then disconnects. The task never faults: a command's failure is recorded in the operation's
    /// scope for the operation to report, since the reader can't know the exit status before the output ends.
    /// </summary>
    public async Task CloseAsync()
    {
        try
        {
            SshCommandIo[] started;
            lock (commands)
            {
                started = [.. commands];
            }

            foreach (var command in started)
            {
                if (await command.EndAsync().ConfigureAwait(false) is { } failure)
                {
                    scope.Record(failure);
                }
            }
        }
        finally
        {
            await watchdog.DisposeAsync().ConfigureAwait(false);
            client.Dispose();
        }
    }

    private static GitFailureException Classify(SshAddress address, Exception ex) => ex switch
    {
        SshAuthenticationException => new GitFailureException(SyncFailure.Authentication, $"{address.Host} refused the device key.", ex),
        _ => new GitFailureException(SyncFailure.Connection, $"Can't reach {address.Host}:{address.Port}: {ex.Message}", ex),
    };

    // Quotes the path the way git does for the POSIX shell that git hosts run.
    private static string Quote(string path) =>
        "'" + path.Replace("'", @"'\''", StringComparison.Ordinal).Replace("!", @"'\!'", StringComparison.Ordinal) + "'";
}
