using System.Security.Cryptography;

namespace Memoria.ToDo.Desktop.App.Tests;

internal sealed class FakeUserProtection : IUserProtection, IDisposable
{
    private byte[] secret = [];
    public byte[] Input { get; private set; } = [];
    public byte[] Unprotected { get; private set; } = [];
    public bool RefuseOpening { get; set; }
    public bool RefuseProtection { get; set; }

    public byte[] Protect(byte[] value)
    {
        if (RefuseProtection)
        {
            throw new System.IO.IOException("This Windows account cannot protect the key.");
        }
        Input = value;
        secret = value.ToArray();
        return [23, 42, 99];
    }

    public byte[] Unprotect(byte[] value)
    {
        if (RefuseOpening || !value.AsSpan().SequenceEqual(new byte[] { 23, 42, 99 }))
        {
            throw new CryptographicException("This Windows account cannot open the key.");
        }
        Unprotected = secret.ToArray();
        return Unprotected;
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(secret);
}
