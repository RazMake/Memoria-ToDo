using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;

namespace Memoria.Proofs;

/// <summary>
/// The proof's RSA 3072 key, stored encrypted with DPAPI for the current Windows user.
/// It's decrypted only in memory, and never written as plain text.
/// </summary>
internal static class ProofKey
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Memoria-ToDo-proofs",
        "keys",
        "proof-rsa3072.dpapi");

    public static void Create()
    {
        if (File.Exists(FilePath))
        {
            throw new InvalidOperationException($"A proof key already exists: {FilePath}. Delete it to make a new one.");
        }

        using var rsa = RSA.Create(3072);
        byte[] pem = Encoding.ASCII.GetBytes(rsa.ExportRSAPrivateKeyPem());
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, ProtectedData.Protect(pem, null, DataProtectionScope.CurrentUser));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pem);
        }
    }

    public static Loaded Load()
    {
        if (!File.Exists(FilePath))
        {
            throw new FileNotFoundException("There's no proof key yet. Run 'keygen' first.", FilePath);
        }

        byte[] pem = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
        char[] chars = Encoding.ASCII.GetChars(pem);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(chars);
            byte[] blob = OpenSshRsa.PublicKeyBlob(rsa.ExportParameters(includePrivateParameters: false));
            return new Loaded(new PrivateKeyFile(new MemoryStream(pem, writable: false)), blob);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pem);
            Array.Clear(chars);
        }
    }

    internal sealed record Loaded(PrivateKeyFile Key, byte[] PublicKeyBlob)
    {
        public string PublicKeyLine => OpenSshRsa.PublicKeyLine(PublicKeyBlob, "memoria-proof");

        public string Fingerprint => SshTransport.Fingerprint(PublicKeyBlob);
    }
}
