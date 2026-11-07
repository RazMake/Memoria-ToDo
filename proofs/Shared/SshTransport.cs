using System.Diagnostics;
using System.Security.Cryptography;
using LibGit2Sharp;
using Renci.SshNet;
using Renci.SshNet.Security;

namespace Memoria.Proofs;

/// <summary>
/// Git over SSH for LibGit2Sharp, built on SSH.NET. LibGit2Sharp creates the transport itself,
/// so the key and the pinned host keys are set here, once, before any clone, fetch, or push.
/// </summary>
public static class SshTransport
{
    private static readonly Lock Gate = new();
    private static bool registered;

    internal static IPrivateKeySource? Key { get; private set; }

    /// <summary>Host name to the SHA256 fingerprints (unpadded base64) of the host keys it may present.</summary>
    internal static IReadOnlyDictionary<string, string[]> HostKeyPins { get; private set; } =
        new Dictionary<string, string[]>();

    public static Action<string> Log { get; set; } = static _ => { };

    public static void Use(IPrivateKeySource key, IReadOnlyDictionary<string, string[]> hostKeyPins)
    {
        Key = new RsaSha2Only(key);
        HostKeyPins = hostKeyPins;
        lock (Gate)
        {
            if (!registered)
            {
                GlobalSettings.RegisterSmartSubtransport<SshSubtransport>("ssh");
                registered = true;
            }
        }
    }

    public static string Fingerprint(byte[] publicKeyBlob) =>
        Convert.ToBase64String(SHA256.HashData(publicKeyBlob)).TrimEnd('=');

    internal static SshClient Connect(SshAddress address)
    {
        var key = Key ?? throw new InvalidOperationException("Call SshTransport.Use before git uses SSH.");
        var info = new ConnectionInfo(
            address.Host,
            address.Port,
            address.User,
            new PrivateKeyAuthenticationMethod(address.User, key))
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        var client = new SshClient(info);
        string? rejected = null;
        client.HostKeyReceived += (_, e) =>
        {
            string fingerprint = Fingerprint(e.HostKey);
            e.CanTrust = HostKeyPins.TryGetValue(address.Host, out var pins) && pins.Contains(fingerprint);
            if (!e.CanTrust)
            {
                rejected = $"{e.HostKeyName} SHA256:{fingerprint}";
            }
        };

        long started = Stopwatch.GetTimestamp();
        try
        {
            client.Connect();
        }
        catch (Exception ex) when (rejected is not null)
        {
            client.Dispose();
            throw new IOException($"{address.Host} presented a host key that isn't pinned: {rejected}.", ex);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        Log($"  ssh {address.User}@{address.Host}:{address.Port} connected in " +
            $"{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms " +
            $"({info.CurrentKeyExchangeAlgorithm}, {info.CurrentHostKeyAlgorithm}, {info.CurrentServerEncryption})");
        return client;
    }

    // SSH.NET offers an RSA key as ssh-rsa (SHA-1) first. Hosts reject or block SHA-1 signatures,
    // and every refused offer costs a round trip, so only the SHA-2 forms are offered.
    private sealed class RsaSha2Only(IPrivateKeySource key) : IPrivateKeySource
    {
        public IReadOnlyCollection<HostAlgorithm> HostKeyAlgorithms { get; } =
            key.HostKeyAlgorithms.Where(a => a.Name != "ssh-rsa").ToList();
    }
}

/// <summary>
/// One libgit2 transport: one SSH connection per connect, one command per service.
/// libgit2 treats SSH as stateful, so the pack exchange reuses the stream that listed the refs.
/// </summary>
internal sealed class SshSubtransport : SmartSubtransport
{
    private SshClient? client;
    private SshCommandStream? stream;

    protected override SmartSubtransportStream Action(string url, GitSmartSubtransportAction action)
    {
        try
        {
            return action switch
            {
                GitSmartSubtransportAction.UploadPackList => Start(url, "git-upload-pack"),
                GitSmartSubtransportAction.ReceivePackList => Start(url, "git-receive-pack"),
                GitSmartSubtransportAction.UploadPack or GitSmartSubtransportAction.ReceivePack =>
                    stream ?? throw new InvalidOperationException("git asked for a pack exchange before listing the refs."),
                _ => throw new NotSupportedException($"git asked for an unknown SSH action: {action}."),
            };
        }
        catch (Exception ex)
        {
            // libgit2 keeps only the message, so the full cause is logged here.
            SshTransport.Log($"  transport failed: {ex}");
            throw;
        }
    }

    protected override void Close()
    {
        // libgit2 frees the stream itself, before it closes the transport.
        stream = null;
        if (client is not null)
        {
            long started = Stopwatch.GetTimestamp();
            client.Dispose();
            SshTransport.Log($"  ssh disconnected in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        }

        client = null;
        base.Close();
    }

    private SshCommandStream Start(string url, string service)
    {
        var address = SshAddress.Parse(url);
        client ??= SshTransport.Connect(address);
        stream = new SshCommandStream(this, client.CreateCommand($"{service} {Quote(address.Path)}"));
        return stream;
    }

    // Quotes the path the way git does for a POSIX shell, which is what git hosts parse.
    private static string Quote(string path) =>
        "'" + path.Replace("'", @"'\''", StringComparison.Ordinal).Replace("!", @"'\!'", StringComparison.Ordinal) + "'";
}

internal sealed class SshCommandStream : SmartSubtransportStream
{
    private readonly SshCommand command;
    private readonly Task execution;
    private readonly Stream input;
    private readonly byte[] readBuffer = new byte[64 * 1024];
    private readonly byte[] writeBuffer = new byte[64 * 1024];
    private readonly long started = Stopwatch.GetTimestamp();
    private bool readAny;

    public SshCommandStream(SmartSubtransport owner, SshCommand command)
        : base(owner)
    {
        this.command = command;
        execution = command.ExecuteAsync();
        input = command.CreateInputStream();
        SshTransport.Log($"  ssh exec {command.CommandText} started in {Elapsed} ms");
    }

    private string Elapsed => Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F0");

    public override int Read(Stream dataStream, long length, out long bytesRead)
    {
        int read = command.OutputStream.Read(readBuffer, 0, (int)Math.Min(length, readBuffer.Length));
        if (!readAny)
        {
            readAny = true;
            SshTransport.Log($"  first output at {Elapsed} ms");
        }

        if (read == 0)
        {
            SshTransport.Log($"  output ended at {Elapsed} ms");
            ThrowIfFailed();
        }

        dataStream.Write(readBuffer, 0, read);
        bytesRead = read;
        return 0;
    }

    public override int Write(Stream dataStream, long length)
    {
        for (long left = length; left > 0;)
        {
            int read = dataStream.Read(writeBuffer, 0, (int)Math.Min(left, writeBuffer.Length));
            if (read == 0)
            {
                throw new EndOfStreamException("libgit2 gave fewer bytes than it asked to write.");
            }

            input.Write(writeBuffer, 0, read);
            left -= read;
        }

        input.Flush();
        return 0;
    }

    protected override void Free()
    {
        SshTransport.Log($"  stream freed at {Elapsed} ms");

        // Like git with OpenSSH: send EOF, then let the server end the command before closing the
        // channel. Closing first makes SSH.NET wait for the server's close, up to a second each time.
        input.Dispose();
        try
        {
            if (!execution.Wait(EndWait))
            {
                SshTransport.Log($"  '{command.CommandText}' didn't end within {EndWait.TotalSeconds:F0} s of EOF");
            }
        }
        catch (AggregateException ex)
        {
            SshTransport.Log($"  '{command.CommandText}' ended with an error: {ex.InnerException?.Message}");
        }

        SshTransport.Log($"  command ended at {Elapsed} ms, exit status {command.ExitStatus?.ToString() ?? "none"}");
        command.Dispose();
        SshTransport.Log($"  command disposed at {Elapsed} ms");
        base.Free();
    }

    private static readonly TimeSpan EndWait = TimeSpan.FromSeconds(5);

    // The output ends only when the channel closes or the connection fails; both finish the execution.
    private void ThrowIfFailed()
    {
        try
        {
            execution.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new IOException($"'{command.CommandText}' failed: {ex.Message}", ex);
        }

        if (command.ExitStatus is int status and not 0)
        {
            throw new IOException($"'{command.CommandText}' exited with {status}: {command.Error.Trim()}");
        }
    }
}
