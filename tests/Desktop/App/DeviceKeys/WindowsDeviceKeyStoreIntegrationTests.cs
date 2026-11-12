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
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
