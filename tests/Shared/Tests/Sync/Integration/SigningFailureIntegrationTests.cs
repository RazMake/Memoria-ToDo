using System.Security.Cryptography;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

[Integration]
public sealed class SigningFailureIntegrationTests
{
    [Fact]
    public async Task A_device_key_that_cannot_sign_fails_the_clone_as_a_storage_error_not_as_offline_when_the_host_is_trusted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var inner = FakeDeviceKeyStoreWithSeededKey();
        var a = world.AddDevice("a", syncKeys: new UnsignableKeyStore(inner, new CryptographicException("The key can't sign.")));
        Directory.CreateDirectory(a.DeviceFolder);
        await File.WriteAllTextAsync(
            Path.Combine(a.DeviceFolder, "known-hosts.json"),
            $$"""{"version":1,"hosts":[{"host":"127.0.0.1","port":{{world.Server.Port}},"fingerprint":"{{world.Server.HostKeyFingerprint}}"}]}""",
            ct);

        await Assert.ThrowsAsync<IOException>(() => a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, ct));

        Assert.Equal(SyncState.Error, a.Sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, a.Sync.Status.Failure);
        Assert.Contains("The key can't sign.", a.Sync.Status.Details);
        Assert.False(a.Sync.Setup.State.IsConfigured);
    }

    private static IDeviceKeyStore FakeDeviceKeyStoreWithSeededKey()
    {
        var keys = Fakes.FakeDeviceKeyStore.Signing();
        keys.SeedKey();
        return keys;
    }

    /// <summary>Hands out the inner store's keys with the same public key, but signing always fails.</summary>
    private sealed class UnsignableKeyStore(IDeviceKeyStore inner, Exception failure) : IDeviceKeyStore
    {
        public bool HasKey => inner.HasKey;

        public IDeviceSigningKey Generate() => inner.Generate();

        public void Delete() => inner.Delete();

        public IDeviceSigningKey Open() => new Unsignable(inner.Open(), failure);
    }

    private sealed class Unsignable(IDeviceSigningKey key, Exception failure) : IDeviceSigningKey
    {
        public ReadOnlyMemory<byte> PublicKeyBlob => key.PublicKeyBlob;

        public byte[] Sign(ReadOnlySpan<byte> data, RsaSignatureAlgorithm algorithm) => throw failure;

        public void Dispose() => key.Dispose();
    }
}
