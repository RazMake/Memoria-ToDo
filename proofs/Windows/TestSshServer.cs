using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Algorithms;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;

namespace Memoria.Proofs;

/// <summary>
/// An in-process SSH server that serves the bare repositories under one folder by running the
/// installed git. It accepts one client key and only git-upload-pack and git-receive-pack.
/// </summary>
internal sealed class TestSshServer : IDisposable
{
    private readonly SshServer server;
    private readonly string root;
    private readonly byte[] clientKey;
    private readonly Action<string> log;

    public TestSshServer(string root, byte[] clientKey, Action<string> log)
    {
        this.root = Path.GetFullPath(root);
        this.clientKey = clientKey;
        this.log = log;

        var trace = new TraceSource("test-ssh-server", SourceLevels.Warning);
        trace.Listeners.Clear();
        trace.Listeners.Add(new LogListener(log));
        server = new SshServer(SshSessionConfiguration.Default, trace);

        var hostKey = SshAlgorithms.PublicKey.RsaWithSha512.GenerateKeyPair();
        server.Credentials = new[] { hostKey };
        HostKeyFingerprint = SshTransport.Fingerprint(hostKey.GetPublicKeyBytes().ToArray());

        server.SessionAuthenticating += OnAuthenticating;
        server.ChannelRequest += OnChannelRequest;
        server.ExceptionRaised += (_, ex) => log($"  server error: {ex.Message}");
    }

    public string HostKeyFingerprint { get; }

    /// <summary>Starts listening and returns the port, once the listener is bound.</summary>
    public async Task<int> ListenAsync(IPAddress address, int port)
    {
        var bound = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.TcpListenerFactory = new ListenerFactory(bound);
        Task accepting = server.AcceptSessionsAsync(port, address);
        _ = accepting.ContinueWith(
            t => bound.TrySetException(t.Exception!.InnerExceptions),
            TaskContinuationOptions.OnlyOnFaulted);
        return await bound.Task;
    }

    public void Dispose() => server.Dispose();

    private void OnAuthenticating(object? sender, SshAuthenticatingEventArgs e)
    {
        bool allowed =
            e.AuthenticationType is SshAuthenticationType.ClientPublicKeyQuery or SshAuthenticationType.ClientPublicKey
            && e.PublicKey is { } key
            && key.GetPublicKeyBytes().ToArray().AsSpan().SequenceEqual(clientKey);
        e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(
            allowed ? new ClaimsPrincipal(new ClaimsIdentity("publickey")) : null);
    }

    private void OnChannelRequest(object? sender, SshRequestEventArgs<ChannelRequestMessage> e)
    {
        if (sender is not SshChannel channel || e.RequestType != ChannelRequestTypes.Command)
        {
            return;
        }

        // Subscribe to the channel's data now, before the client can send any.
        var stream = new SshStream(channel);
        e.IsAuthorized = true;
        _ = RunAsync(channel, stream, e.Request.ConvertTo<CommandRequestMessage>().Command!);
    }

    private async Task RunAsync(SshChannel channel, SshStream stream, string command)
    {
        uint exitCode = 128;
        try
        {
            if (!TryResolve(command, out string verb, out string repository))
            {
                log($"  server refused: {command}");
                return;
            }

            var start = new ProcessStartInfo("git")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(verb);
            start.ArgumentList.Add(repository);

            long started = Stopwatch.GetTimestamp();
            using var git = Process.Start(start)!;
            Task<string> errors = git.StandardError.ReadToEndAsync();
            _ = CopyInputAsync(stream, git.StandardInput.BaseStream);
            await git.StandardOutput.BaseStream.CopyToAsync(stream);
            await git.WaitForExitAsync();
            exitCode = (uint)git.ExitCode;
            log($"  server git {verb} exited with {exitCode} after {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");

            string error = (await errors).Trim();
            if (error.Length > 0)
            {
                log($"  server git {verb}: {error}");
            }
        }
        catch (Exception ex)
        {
            log($"  server failed to run '{command}': {ex.Message}");
        }
        finally
        {
            await channel.CloseAsync(exitCode);
            stream.Dispose();
        }
    }

    // The client may write after git exits (libgit2 says goodbye with a flush packet), so a
    // failed copy into a closed pipe is expected and only logged.
    private async Task CopyInputAsync(Stream from, Stream to)
    {
        try
        {
            await from.CopyToAsync(to);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            log($"  server input ended early: {ex.Message}");
        }
        finally
        {
            to.Dispose();
        }
    }

    private bool TryResolve(string command, out string verb, out string repository)
    {
        verb = repository = string.Empty;
        int space = command.IndexOf(' ');
        if (space < 0)
        {
            return false;
        }

        verb = command[..space] switch
        {
            "git-upload-pack" => "upload-pack",
            "git-receive-pack" => "receive-pack",
            _ => string.Empty,
        };
        string quoted = command[(space + 1)..];
        if (verb.Length == 0 || quoted.Length < 2 || quoted[0] != '\'' || quoted[^1] != '\'')
        {
            return false;
        }

        string path = quoted[1..^1]
            .Replace(@"'\''", "'", StringComparison.Ordinal)
            .Replace(@"'\!'", "!", StringComparison.Ordinal)
            .TrimStart('/');
        repository = Path.GetFullPath(Path.Combine(root, path));
        return repository.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(repository);
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

    private sealed class LogListener(Action<string> log) : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
            log($"  server {eventType}: {message?.Split('\n', 2)[0].TrimEnd()}");
    }
}
