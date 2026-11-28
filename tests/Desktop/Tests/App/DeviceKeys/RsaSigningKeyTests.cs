using System.Security.Cryptography;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class RsaSigningKeyTests
{
    [Fact]
    public void The_public_key_blob_is_ssh_rsa_then_the_exponent_and_the_modulus_with_a_zero_byte_before_a_high_bit()
    {
        using var rsa = RSA.Create(1024);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        using var key = new RsaSigningKey(rsa);

        var expected = Field("ssh-rsa"u8.ToArray())
            .Concat(Field(parameters.Exponent!))
            .Concat(Field([0, .. parameters.Modulus!]))
            .ToArray();

        Assert.True(parameters.Modulus![0] >= 0x80, "A 1024-bit modulus always has its high bit set.");
        Assert.Equal(expected, key.PublicKeyBlob.ToArray());
    }

    [Fact]
    public void Signing_with_an_unsupported_algorithm_is_rejected()
    {
        using var key = new RsaSigningKey(RSA.Create(1024));

        Assert.Throws<ArgumentOutOfRangeException>(() => key.Sign([1, 2, 3], (RsaSignatureAlgorithm)99));
    }

    private static byte[] Field(byte[] value) =>
        [(byte)(value.Length >> 24), (byte)(value.Length >> 16), (byte)(value.Length >> 8), (byte)value.Length, .. value];
}
