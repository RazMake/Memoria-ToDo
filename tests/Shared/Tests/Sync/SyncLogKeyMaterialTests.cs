using System.Security.Cryptography;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// Sync never logs key material. A key store here holds a recognizable private key, passphrase, key file, and signature in
/// public properties and in <c>ToString()</c>, so a log that wrote the key object, its properties, or a signature would
/// show them. Every key operation runs, and no entry's message, values, or exception text may contain any of them.
/// </summary>
[Unit]
public sealed class SyncLogKeyMaterialTests
{
    private const string Pem = "-----BEGIN PRIVATE KEY-----\nQUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVo=\n-----END PRIVATE KEY-----";
    private const string Passphrase = "correct-horse-battery-staple";
    private static readonly byte[] KeyFile = "KEYFILE-BYTES-0123456789"u8.ToArray();
    private static readonly byte[] Signature = "SIGNATURE-BYTES-9876543210"u8.ToArray();

    private readonly SyncFixture f = new();

    public static TheoryData<string> Operations() => new()
    {
        "generating the key",
        "forgetting the key",
        "setting the device name",
        "using an existing clone",
        "cloning",
        "accepting a host key",
        "a successful pass",
        "a failed pass",
        "an offline pass",
        "a key that cannot be opened",
    };

    private static IEnumerable<string> Secrets() =>
    [
        Pem,
        "QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVo=",
        Passphrase,
        Encoding.UTF8.GetString(KeyFile),
        Convert.ToBase64String(KeyFile),
        Convert.ToHexString(KeyFile),
        Encoding.UTF8.GetString(Signature),
        Convert.ToBase64String(Signature),
        Convert.ToHexString(Signature),
    ];

    private ISyncService Service(SecretKeyStore keys, SigningGit git) =>
        SyncService.Create(f.Repository, f.Tasks, f.Contacts, f.Files, keys, f.Selection, f.Time, f.DeviceFolder, SyncFixture.DefaultDeviceName, null, git, f.Logs.CreateLogger());

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Logs_no_key_material_while(string operation)
    {
        var keys = new SecretKeyStore(f.Keys);
        var git = new SigningGit(f.Git);
        await using var sync = Service(keys, git);
        var ct = CancellationToken.None;

        switch (operation)
        {
            case "generating the key":
                await sync.Setup.GenerateKeyAsync(ct);
                break;
            case "forgetting the key":
                f.Keys.SeedKey();
                await sync.Setup.ForgetKeyAsync(ct);
                break;
            case "setting the device name":
                f.Keys.SeedKey();
                await sync.Setup.SetDeviceNameAsync("Desk", ct);
                break;
            case "using an existing clone":
                f.Keys.SeedKey();
                await sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, ct);
                break;
            case "cloning":
                f.Keys.SeedKey();
                await sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, ct);
                break;
            case "accepting a host key":
                f.Keys.SeedKey();
                f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
                await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, ct));
                await sync.Setup.AcceptHostKeyAsync("SHA256:first", ct);
                break;
            case "a successful pass":
                f.Ready();
                await sync.SyncNowAsync(ct);
                break;
            case "a failed pass":
                f.Ready();
                f.Git.ScriptPass().Fail(new GitFailureException(SyncFailure.Authentication, "The host refused the key."));
                await sync.SyncNowAsync(ct);
                break;
            case "an offline pass":
                f.Ready();
                f.Git.ScriptPass().Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
                await sync.SyncNowAsync(ct);
                break;
            default:
                f.Ready();
                f.Keys.OpenFailure = new IOException("The stored key can't be opened.");
                await sync.SyncNowAsync(ct);
                break;
        }

        Assert.NotEmpty(f.Logs.Events);
        foreach (var entry in f.Logs.Events)
        {
            var text = entry.EverythingIn();
            foreach (var secret in Secrets())
                Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }

    /// <summary>Hands out the inner fake store's keys wrapped so each one carries the secrets.</summary>
    private sealed class SecretKeyStore(FakeDeviceKeyStore inner) : IDeviceKeyStore
    {
        public bool HasKey => inner.HasKey;

        public IDeviceSigningKey Generate() => new SecretKey(inner.Generate());

        public IDeviceSigningKey Open() => new SecretKey(inner.Open());

        public void Delete() => inner.Delete();
    }

    private sealed class SecretKey(IDeviceSigningKey inner) : IDeviceSigningKey
    {
        public string PrivateKeyPem => Pem;

        public string KeyPassphrase => Passphrase;

        public byte[] KeyFileBytes => KeyFile;

        public ReadOnlyMemory<byte> PublicKeyBlob => inner.PublicKeyBlob;

        public byte[] Sign(ReadOnlySpan<byte> data, RsaSignatureAlgorithm algorithm) => Signature;

        public void Dispose() => inner.Dispose();

        public override string ToString() => $"{Pem} {Passphrase} {Encoding.UTF8.GetString(KeyFile)} {Convert.ToBase64String(Signature)}";
    }

    /// <summary>The fake Git operations, which also sign with the connection's key, as the real transport does.</summary>
    private sealed class SigningGit(FakeGitOperations inner) : IGitOperations
    {
        public GitReceipt? ReadReceipt(string folder) => inner.ReadReceipt(folder);

        public void WriteReceipt(string folder, GitReceipt receipt) => inner.WriteReceipt(folder, receipt);

        public string? ReadOrigin(string folder) => inner.ReadOrigin(folder);

        public void UseExisting(string folder, SshAddress address) => inner.UseExisting(folder, address);

        public Task Clone(GitConnection connection, SshAddress address, string folder, CancellationToken cancellationToken)
        {
            connection.Key.Sign("challenge"u8, RsaSignatureAlgorithm.Sha256);
            return inner.Clone(connection, address, folder, cancellationToken);
        }

        public Task<GitPassResult> RunPass(GitConnection connection, string folder, CancellationToken cancellationToken)
        {
            connection.Key.Sign("challenge"u8, RsaSignatureAlgorithm.Sha256);
            return inner.RunPass(connection, folder, cancellationToken);
        }
    }
}
