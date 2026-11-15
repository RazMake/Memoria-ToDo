using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

internal static class DevicePublicKey
{
    public static RSA Read(byte[] blob)
    {
        using var input = new MemoryStream(blob);
        using var reader = new BinaryReader(input);
        byte[] Field()
        {
            var count = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            return reader.ReadBytes(count);
        }
        Assert.Equal("ssh-rsa", Encoding.ASCII.GetString(Field()));
        var exponent = Field();
        var modulus = Field();
        // RFC 4251 mpint: never a set top bit without a leading zero, and no zero that isn't needed.
        Assert.True(modulus[0] < 0x80, "The modulus must not look negative.");
        Assert.True(modulus[0] != 0 || modulus[1] >= 0x80, "The modulus must not start with an unneeded zero.");
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Exponent = exponent,
            Modulus = modulus[0] == 0 ? modulus[1..] : modulus
        });
        return rsa;
    }
}
