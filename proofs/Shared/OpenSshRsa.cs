using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Memoria.Proofs;

/// <summary>OpenSSH encodings of an RSA public key.</summary>
public static class OpenSshRsa
{
    /// <summary>The SSH wire form: string "ssh-rsa", mpint e, mpint n.</summary>
    public static byte[] PublicKeyBlob(RSAParameters key)
    {
        using var blob = new MemoryStream();
        WriteBytes(blob, "ssh-rsa"u8);
        WriteMpint(blob, key.Exponent ?? throw new ArgumentException("The key has no exponent.", nameof(key)));
        WriteMpint(blob, key.Modulus ?? throw new ArgumentException("The key has no modulus.", nameof(key)));
        return blob.ToArray();
    }

    /// <summary>The line that goes into authorized_keys, GitHub, or Azure DevOps.</summary>
    public static string PublicKeyLine(byte[] blob, string comment) =>
        $"ssh-rsa {Convert.ToBase64String(blob)} {comment}";

    /// <summary>The SSH signature form: string algorithm name, string signature (RFC 8332).</summary>
    public static byte[] SignatureBlob(string algorithm, byte[] signature)
    {
        using var blob = new MemoryStream();
        WriteBytes(blob, System.Text.Encoding.ASCII.GetBytes(algorithm));
        WriteBytes(blob, signature);
        return blob.ToArray();
    }

    private static void WriteMpint(Stream to, ReadOnlySpan<byte> unsignedBigEndian)
    {
        unsignedBigEndian = unsignedBigEndian.TrimStart((byte)0);
        bool pad = unsignedBigEndian.Length > 0 && (unsignedBigEndian[0] & 0x80) != 0;
        WriteLength(to, unsignedBigEndian.Length + (pad ? 1 : 0));
        if (pad)
        {
            to.WriteByte(0);
        }

        to.Write(unsignedBigEndian);
    }

    private static void WriteBytes(Stream to, ReadOnlySpan<byte> bytes)
    {
        WriteLength(to, bytes.Length);
        to.Write(bytes);
    }

    private static void WriteLength(Stream to, int length)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, length);
        to.Write(buffer);
    }
}
