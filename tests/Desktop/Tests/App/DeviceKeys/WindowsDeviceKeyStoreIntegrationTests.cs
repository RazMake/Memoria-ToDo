using System.IO;
using System.Security.Cryptography;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Integration]
public sealed class WindowsDeviceKeyStoreIntegrationTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("memoria-device-key-").FullName;

    [Fact]
    public void The_Windows_account_can_reopen_its_persisted_key_after_the_original_handle_closes()
    {
        var files = new WindowsFileSystem();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, new WindowsUserProtection(), folder);
        byte[] publicKey;
        using (var generated = keys.Generate())
        {
            publicKey = generated.PublicKeyBlob.ToArray();
        }

        using var reopened = new WindowsDeviceKeyStore(files, new WindowsUserProtection(), folder).Open();

        Assert.Equal(publicKey, reopened.PublicKeyBlob.ToArray());
        var data = new byte[] { 1, 2, 3 };
        var signature = reopened.Sign(data, RsaSignatureAlgorithm.Sha256);
        using var publicRsa = DevicePublicKey.Read(publicKey);
        Assert.True(publicRsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.Single(Directory.EnumerateFiles(folder));
        Assert.Equal(3072, publicRsa.KeySize);
        var sha512 = reopened.Sign(data, RsaSignatureAlgorithm.Sha512);
        Assert.True(publicRsa.VerifyData(data, sha512, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Deleting_removes_the_real_key_file_and_a_new_key_can_then_be_generated()
    {
        var files = new WindowsFileSystem();
        var protection = new WindowsUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, folder, _ => RSA.Create(1024));
        byte[] oldPublicKey;
        using (var generated = keys.Generate())
        {
            oldPublicKey = generated.PublicKeyBlob.ToArray();
        }
        var other = Path.Combine(folder, "device-name.txt");
        File.WriteAllText(other, "Desk");

        keys.Delete();
        keys.Delete();

        Assert.False(keys.HasKey);
        Assert.Equal([other], Directory.EnumerateFiles(folder));
        using var replacement = keys.Generate();
        Assert.NotEqual(oldPublicKey, replacement.PublicKeyBlob.ToArray());
        Assert.True(keys.HasKey);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
