using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Algorithms;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.IO;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Services;
using Microsoft.DevTunnels.Ssh.Tcp;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>How <see cref="TestSshServer"/> ends a command.</summary>
public enum ExitReport
{
    /// <summary>With the command's exit status.</summary>
    Status,

    /// <summary>Closing the channel without any exit status or signal.</summary>
    NoStatus,

    /// <summary>With an exit signal instead of an exit status.</summary>
    Signal,
}

/// <summary>
/// An in-process SSH server on the loopback address that serves the bare repositories under one folder by running the
/// installed git. It accepts one client key and only git-upload-pack and git-receive-pack. A test can run code before
/// each command, to count it, hold it, or let another device push first.
/// </summary>
internal sealed class TestSshServer : IDisposable
{
    private readonly SshServer server;
    private readonly string root;
    private readonly byte[] clientKey;
    private readonly List<string> commands = [];

    public TestSshServer(string root, byte[] clientKey)
    {
        this.root = Path.GetFullPath(root);
        this.clientKey = clientKey;

        var trace = new TraceSource("test-ssh-server", SourceLevels.Off);
        trace.Listeners.Clear();
        server = new SshServer(SshSessionConfiguration.Default, trace);

        var hostKey = SshAlgorithms.PublicKey.RsaWithSha512.GenerateKeyPair();
        server.Credentials = new[] { hostKey };
        HostKeyFingerprint = "SHA256:" + Convert.ToBase64String(SHA256.HashData(hostKey.GetPublicKeyBytes().ToArray())).TrimEnd('=');

        server.SessionAuthenticating += OnAuthenticating;
        server.ChannelRequest += OnChannelRequest;
        server.ChannelOpening += OnChannelOpening;
    }

    // The library waits for this task before confirming the open, so the client's channel open stays pending until the hook returns.
    // Its result must be an open confirmation (or failure); the library fills in the channel numbers and window sizes itself.
    private void OnChannelOpening(object? sender, SshChannelOpeningEventArgs e)
    {
        if (BeforeChannelOpen is { } before)
            e.OpeningTask = ConfirmAfterAsync(before);
    }

    private static async Task<ChannelMessage> ConfirmAfterAsync(Func<Task> before)
    {
        await before();
        return new ChannelOpenConfirmationMessage();
    }

    /// <summary>The fingerprint of this server's host key, as Sync shows and compares it.</summary>
    public string HostKeyFingerprint { get; }

    public int Port { get; private set; }

    /// <summary>Runs before git serves a command, with the verb (<c>upload-pack</c> or <c>receive-pack</c>) and the bare repository's folder.</summary>
    public Func<string, string, Task>? BeforeCommand { get; set; }

    /// <summary>
    /// Whether the accepted key may push. When false, the server closes <c>receive-pack</c> without advertising anything,
    /// as a host does for a read-only deploy key. Fetching keeps working.
    /// </summary>
    public bool AllowPush { get; set; } = true;

    /// <summary>The exit status a refused <c>receive-pack</c> (see <see cref="AllowPush"/>) reports. 128 by default; 0 is a host that closes quietly, which proves nothing about write access.</summary>
    public uint RefusedExitCode { get; set; } = 128;

    /// <summary>The text a refused <c>receive-pack</c> (see <see cref="AllowPush"/>) sends on the command's error output, as a host explains a refusal. Null sends nothing.</summary>
    public string? DenialText { get; set; }

    /// <summary>
    /// The exit status to report, for the verb, after git served the whole command successfully (a hook that rejects late,
    /// or a host that fails after the protocol finished). Null keeps git's own status.
    /// </summary>
    public Func<string, uint?>? ExitCodeAfterServing { get; set; }

    /// <summary>Whether to refuse the command request for the verb outright, as a host that closes the session before running anything. Null accepts every command.</summary>
    public Func<string, bool>? RefuseCommand { get; set; }

    /// <summary>How the server ends a command: with its exit status, as every host does, or without one.</summary>
    public ExitReport Report { get; set; } = ExitReport.Status;

    /// <summary>Runs after git's output was fully sent and before the command's exit status and close, with the verb.</summary>
    public Func<string, Task>? BeforeExit { get; set; }

    /// <summary>Runs after the client asks to open a channel and before the server accepts it, to hold the open itself, before any command exists.</summary>
    public Func<Task>? BeforeChannelOpen { get; set; }

    /// <summary>The verbs of every command served so far, in order.</summary>
    public IReadOnlyList<string> Commands
    {
        get { lock (commands) { return [.. commands]; } }
    }

    /// <summary>Starts listening on a free port, and returns once the listener is bound.</summary>
    public async Task StartAsync()
    {
        var bound = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.TcpListenerFactory = new ListenerFactory(bound);
        var accepting = server.AcceptSessionsAsync(0, IPAddress.Loopback);
        _ = accepting.ContinueWith(
            t => bound.TrySetException(t.Exception!.InnerExceptions),
            TaskContinuationOptions.OnlyOnFaulted);
        Port = await bound.Task;
    }

    public void Dispose() => server.Dispose();

    private void OnAuthenticating(object? sender, SshAuthenticatingEventArgs e)
    {
        var allowed =
            e.AuthenticationType is SshAuthenticationType.ClientPublicKeyQuery or SshAuthenticationType.ClientPublicKey
            && e.PublicKey is { } key
            && key.GetPublicKeyBytes().ToArray().AsSpan().SequenceEqual(clientKey);
        e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(
            allowed ? new ClaimsPrincipal(new ClaimsIdentity("publickey")) : null);
    }

    private void OnChannelRequest(object? sender, SshRequestEventArgs<ChannelRequestMessage> e)
    {
        if (sender is not SshChannel channel || e.RequestType != ChannelRequestTypes.Command)
            return;

        var command = e.Request.ConvertTo<CommandRequestMessage>().Command!;
        if (RefuseCommand is { } refuse && TryResolve(command, out var requested, out _) && refuse(requested))
        {
            e.IsAuthorized = false;
            return;
        }

        // Subscribe to the channel's data now, before the client can send any.
        var stream = new SshStream(channel);
        e.IsAuthorized = true;
        _ = RunAsync(channel, stream, command);
    }

    private async Task RunAsync(SshChannel channel, SshStream stream, string command)
    {
        uint exitCode = 128;
        try
        {
            if (!TryResolve(command, out var verb, out var repository))
                return;

            lock (commands) { commands.Add(verb); }
            if (verb == "receive-pack" && !AllowPush)
            {
                if (DenialText is { } denial)
                    await new ErrorOutputSender(channel.Session).SendAsync(channel, denial);
                exitCode = RefusedExitCode;
                return;
            }

            if (BeforeCommand is { } before)
                await before(verb, repository);

            var start = new ProcessStartInfo("git")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(verb);
            start.ArgumentList.Add(repository);

            using var git = Process.Start(start)!;
            _ = git.StandardError.ReadToEndAsync();
            _ = CopyInputAsync(stream, git.StandardInput.BaseStream);
            await git.StandardOutput.BaseStream.CopyToAsync(stream);
            await git.WaitForExitAsync();
            exitCode = (uint)git.ExitCode;
            if (exitCode == 0 && ExitCodeAfterServing?.Invoke(verb) is { } overridden)
                exitCode = overridden;
            if (BeforeExit is { } beforeExit)
                await beforeExit(verb);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or SshConnectionException)
        {
            // The client went away; the test that caused it checks what the client saw.
        }
        finally
        {
            try
            {
                await (Report switch
                {
                    ExitReport.NoStatus => channel.CloseAsync(CancellationToken.None),
                    ExitReport.Signal => channel.CloseAsync("KILL", "The host killed the command.", CancellationToken.None),
                    _ => channel.CloseAsync(exitCode),
                });
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or SshConnectionException)
            {
            }

            stream.Dispose();
        }
    }

    // The client may write after git exits (libgit2 says goodbye with a flush packet), so a failed copy into a closed pipe is expected.
    private static async Task CopyInputAsync(Stream from, Stream to)
    {
        try
        {
            await from.CopyToAsync(to);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
        finally
        {
            to.Dispose();
        }
    }

    private bool TryResolve(string command, out string verb, out string repository)
    {
        verb = repository = string.Empty;
        var space = command.IndexOf(' ');
        if (space < 0)
            return false;

        verb = command[..space] switch
        {
            "git-upload-pack" => "upload-pack",
            "git-receive-pack" => "receive-pack",
            _ => string.Empty,
        };
        var quoted = command[(space + 1)..];
        if (verb.Length == 0 || quoted.Length < 2 || quoted[0] != '\'' || quoted[^1] != '\'')
            return false;

        var path = quoted[1..^1]
            .Replace(@"'\''", "'", StringComparison.Ordinal)
            .Replace(@"'\!'", "!", StringComparison.Ordinal)
            .TrimStart('/');
        repository = Path.GetFullPath(Path.Combine(root, path));
        return repository.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(repository);
    }

    // The SSH library sends only standard output for a channel. Error output is the standard extended-data message
    // (RFC 4254 section 5.2, type 95, data type 1), sent through the library's public service extension point.
    private sealed class ErrorOutputSender(SshSession session) : SshService(session)
    {
        public Task SendAsync(SshChannel channel, string text) =>
            SendMessageAsync(
                new ExtendedDataMessage { RecipientChannel = channel.RemoteChannelId, Text = text },
                CancellationToken.None);
    }

    private sealed class ExtendedDataMessage : ChannelMessage
    {
        private const uint StandardError = 1;

        public override byte MessageType => 95;

        public string Text { get; set; } = string.Empty;

        protected override void OnWrite(ref SshDataWriter writer)
        {
            base.OnWrite(ref writer);
            writer.Write(StandardError);
            writer.WriteBinary(Microsoft.DevTunnels.Ssh.Buffer.From(System.Text.Encoding.UTF8.GetBytes(Text)));
        }
    }

    private sealed class ListenerFactory(TaskCompletionSource<int> bound) : ITcpListenerFactory
    {
        public Task<TcpListener> CreateTcpListenerAsync(
            int? remotePort,
            IPAddress localIPAddress,
            int localPort,
            bool canChangeLocalPort,
            TraceSource trace,
            CancellationToken cancellation)
        {
            var listener = new TcpListener(localIPAddress, localPort);
            listener.Start();
            bound.TrySetResult(((IPEndPoint)listener.LocalEndpoint).Port);
            return Task.FromResult(listener);
        }
    }
}
