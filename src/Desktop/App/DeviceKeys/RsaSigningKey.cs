using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.App;

/// <summary>An opened device key. Signs with the RSA key it owns and disposes it; the private key never leaves it.</summary>
internal sealed class RsaSigningKey : IDeviceSigningKey
{
    private readonly RSA _rsa;

    public RsaSigningKey(RSA rsa)
    {
        _rsa = rsa;
        PublicKeyBlob = SshPublicBlob(rsa.ExportParameters(includePrivateParameters: false));
    }

    public ReadOnlyMemory<byte> PublicKeyBlob { get; }

    public byte[] Sign(ReadOnlySpan<byte> data, RsaSignatureAlgorithm algorithm)
    {
        var hash = algorithm switch
        {
            RsaSignatureAlgorithm.Sha256 => HashAlgorithmName.SHA256,
            RsaSignatureAlgorithm.Sha512 => HashAlgorithmName.SHA512,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Only SHA-256 and SHA-512 are supported."),
        };
        return _rsa.SignData(data, hash, RSASignaturePadding.Pkcs1);
    }

    public void Dispose() => _rsa.Dispose();

    // RFC 4253 section 6.6: the "ssh-rsa" name, then the exponent and the modulus as multiple-precision integers.
    private static byte[] SshPublicBlob(RSAParameters publicKey)
    {
        using var blob = new MemoryStream();
        WriteField(blob, Encoding.ASCII.GetBytes("ssh-rsa"));
        WriteInteger(blob, publicKey.Exponent!);
        WriteInteger(blob, publicKey.Modulus!);
        return blob.ToArray();
    }

    private static void WriteInteger(Stream output, byte[] unsignedBigEndian) =>
        WriteField(output, unsignedBigEndian[0] < 0x80 ? unsignedBigEndian : [0, .. unsignedBigEndian]);

    private static void WriteField(Stream output, byte[] value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        output.Write(length);
        output.Write(value);
    }
}
