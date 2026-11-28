using System.Security.Cryptography;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>What SSH.NET gets from the device key: finished signatures, and a classified failure when signing fails.</summary>
[Unit]
public sealed class DeviceKeySourceTests
{
    private static readonly byte[] Data = [1, 2, 3, 4];

    [Fact]
    public void Offers_rsa_sha2_512_and_rsa_sha2_256_only()
    {
        var source = new DeviceKeySource(new FailingKey(new IOException()));

        Assert.Equal(["rsa-sha2-512", "rsa-sha2-256"], source.HostKeyAlgorithms.Select(algorithm => algorithm.Name));
    }

    [Fact]
    public void Signs_with_the_device_key_and_wraps_the_signature_for_the_ssh_wire()
    {
        using var key = FakeDeviceKeyStore.Signing().Generate();
        var source = new DeviceKeySource(key);
        var sha512 = source.HostKeyAlgorithms.First(algorithm => algorithm.Name == "rsa-sha2-512");

        var signature = sha512.Sign(Data);

        Assert.Equal(SshWire.SignatureBlob("rsa-sha2-512", key.Sign(Data, RsaSignatureAlgorithm.Sha512)), signature);
    }

    [Theory]
    [InlineData(typeof(CryptographicException), SyncFailure.LocalStorage, false)]
    [InlineData(typeof(IOException), SyncFailure.LocalStorage, false)]
    [InlineData(typeof(UnauthorizedAccessException), SyncFailure.LocalStorage, false)]
    [InlineData(typeof(ObjectDisposedException), SyncFailure.Unexpected, false)]
    [InlineData(typeof(NotSupportedException), SyncFailure.Unexpected, false)]
    [InlineData(typeof(CryptographicException), SyncFailure.LocalStorage, true)]
    [InlineData(typeof(IOException), SyncFailure.LocalStorage, true)]
    [InlineData(typeof(UnauthorizedAccessException), SyncFailure.LocalStorage, true)]
    [InlineData(typeof(ObjectDisposedException), SyncFailure.Unexpected, true)]
    [InlineData(typeof(NotSupportedException), SyncFailure.Unexpected, true)]
    public void Fails_with_the_classified_failure_keeping_the_cause_when_the_key_cannot_sign_or_cannot_give_its_public_key(Type type, SyncFailure expected, bool readingPublicKey)
    {
        var cause = (Exception)Activator.CreateInstance(type, "The key broke.")!;
        var source = new DeviceKeySource(new FailingKey(cause));

        foreach (var algorithm in source.HostKeyAlgorithms)
        {
            var failure = readingPublicKey
                ? Assert.Throws<GitFailureException>(() => algorithm.Data)
                : Assert.Throws<GitFailureException>(() => algorithm.Sign(Data));

            Assert.Equal(expected, failure.Kind);
            Assert.Equal(cause.Message, failure.Message);
            Assert.Same(cause, failure.InnerException);
        }
    }

    private sealed class FailingKey(Exception failure) : IDeviceSigningKey
    {
        public ReadOnlyMemory<byte> PublicKeyBlob => throw failure;

        public byte[] Sign(ReadOnlySpan<byte> data, RsaSignatureAlgorithm algorithm) => throw failure;

        public void Dispose()
        {
        }
    }
}
