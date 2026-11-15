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

    private readonly IRemoteHost host;
    private readonly OperationScope scope;
    private readonly InactivityWatchdog watchdog;
    private readonly List<SshCommandIo> commands = [];

    internal SshLink(IRemoteHost host, OperationScope scope)
    {
        this.host = host;
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
            host.Disconnect();
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
            var outcome = HostKeyCheck.Run(address.Host, () =>
                connection.VerifyHostKey(address.Host, address.Port, e.HostKeyName, DeviceMetadata.Fingerprint(e.HostKey)));
            e.CanTrust = outcome.CanTrust;
            hostKeyFailure = outcome.Failure;
            if (hostKeyFailure is { Kind: SyncFailure.LocalStorage })
                scope.Record(hostKeyFailure);
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

        return new SshLink(new SshNetRemoteHost(client), scope);
    }

    public SshCommandIo Start(string service, string path, CancellationToken token)
    {
        var command = host.CreateCommand($"{service} {Quote(path)}");
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
    /// Ends every command, then disconnects. The task never faults: the reader can't know the exit status before the
    /// output ends, and the cleanup that awaits this task ignores how it ended, so a command's failure, or a failure to
    /// tear the link down, is recorded in the operation's scope for the operation to report. A failing step doesn't
    /// skip the steps after it.
    /// </summary>
    public async Task CloseAsync()
    {
        SshCommandIo[] started;
        lock (commands)
        {
            started = [.. commands];
        }

        foreach (var command in started)
        {
            try
            {
                if (await command.EndAsync().ConfigureAwait(false) is { } failure)
                {
                    scope.Record(failure);
                }
            }
            catch (Exception ex)
            {
                scope.Record(ex);
            }
        }

        try
        {
            await watchdog.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.Record(ex);
        }

        try
        {
            host.Dispose();
        }
        catch (Exception ex)
        {
            scope.Record(ex);
        }
    }

    // A failure already classified, such as the device key failing to sign, may be wrapped by SSH.NET: it wins.
    internal static GitFailureException Classify(SshAddress address, Exception ex)
    {
        for (var inner = ex; inner is not null; inner = inner.InnerException)
        {
            if (inner is GitFailureException classified)
                return classified;
        }

        if (ex is SshAuthenticationException)
            return new GitFailureException(SyncFailure.Authentication, $"{address.Host} refused the device key.", ex);
        return IsNetworkFailure(ex)
            ? new GitFailureException(SyncFailure.Connection, $"Can't reach {address.Host}:{address.Port}: {ex.Message}", ex)
            : FailureReport.ToLocalFailure(ex);
    }

    // SSH.NET reports a lost or refused connection, a silent host, and a failing proxy as its own exceptions or as a wrapped socket or timeout.
    private static bool IsNetworkFailure(Exception ex)
    {
        for (var inner = ex; inner is not null; inner = inner.InnerException)
        {
            if (inner is SshException or ProxyException or TimeoutException or SocketException)
                return true;
        }

        return false;
    }

    // Quotes the path the way git does for the POSIX shell that git hosts run.
    private static string Quote(string path) =>
        "'" + path.Replace("'", @"'\''", StringComparison.Ordinal).Replace("!", @"'\!'", StringComparison.Ordinal) + "'";
}
