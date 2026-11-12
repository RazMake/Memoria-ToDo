using System.Buffers.Binary;
using System.Text;

namespace Memoria.ToDo.Shared.Sync;

internal static class SshWire
{
    /// <summary>The SSH signature form (RFC 8332): string algorithm name, string raw signature.</summary>
    public static byte[] SignatureBlob(string algorithm, byte[] signature)
    {
        var name = Encoding.ASCII.GetBytes(algorithm);
        var blob = new byte[8 + name.Length + signature.Length];
        BinaryPrimitives.WriteInt32BigEndian(blob, name.Length);
        name.CopyTo(blob, 4);
        BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(4 + name.Length), signature.Length);
        signature.CopyTo(blob, 8 + name.Length);
        return blob;
    }
}
