using System.IO;
using System.Security.Cryptography;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class WindowsDeviceKeyStoreTests
{
    private const string Folder = @"C:\Key tests\device";
    private const string KeyFile = Folder + @"\device-key.json";

    [Fact]
    public void A_missing_key_cannot_be_opened_and_is_not_silently_generated()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);

        Assert.False(keys.HasKey);
        Assert.ThrowsAny<IOException>(() => keys.Open());
        Assert.Empty(files.AllFilesUnder(Folder));
    }

    [Fact]
    public void A_generated_key_reopens_with_the_same_public_identity_and_signs_both_RSA_SHA2_algorithms()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);
        byte[] publicBlob;
        using (var generated = keys.Generate())
        {
            publicBlob = generated.PublicKeyBlob.ToArray();
        }
        Assert.True(keys.HasKey);
        Assert.Equal([KeyFile], files.AllFilesUnder(Folder));
        Assert.All(protection.Input, value => Assert.Equal(0, value));
        var before = files.ReadFile(KeyFile)!.ToArray();

        using var opened = new WindowsDeviceKeyStore(files, protection, Folder).Open();
        Assert.NotEmpty(protection.Unprotected);
        Assert.All(protection.Unprotected, value => Assert.Equal(0, value));

        Assert.Equal(publicBlob, opened.PublicKeyBlob.ToArray());
        using var publicKey = DevicePublicKey.Read(opened.PublicKeyBlob.ToArray());
        Assert.Equal(3072, publicKey.KeySize);
        var data = Encoding.UTF8.GetBytes("A device-authentication challenge");
        foreach (var algorithm in new[] { RsaSignatureAlgorithm.Sha256, RsaSignatureAlgorithm.Sha512 })
        {
            var signature = opened.Sign(data, algorithm);
            Assert.True(publicKey.VerifyData(data, signature,
                algorithm == RsaSignatureAlgorithm.Sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512,
                RSASignaturePadding.Pkcs1));
        }
        Assert.Equal(before, files.ReadFile(KeyFile));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{\"version\":2,\"encryptedPrivateKey\":\"AA==\",\"protectedPassphrase\":\"AA==\"}")]
    public void An_existing_unusable_key_is_never_replaced(string contents)
    {
        var files = new FakeFileSystem();
        var bytes = Encoding.UTF8.GetBytes(contents);
        files.WriteFile(KeyFile, bytes);
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);

        Assert.True(keys.HasKey);
        Assert.ThrowsAny<Exception>(() => keys.Open());
        Assert.Throws<InvalidOperationException>(() => keys.Generate());

        Assert.Equal(bytes, files.ReadFile(KeyFile));
    }

    [Fact]
    public void A_key_Windows_cannot_protect_is_reported_without_creating_a_key_and_a_retry_succeeds()
    {
        const string Other = Folder + @"\other.txt";
        var files = new FakeFileSystem();
        files.WriteFile(Other, [1]);
        using var protection = new FakeUserProtection { RefuseProtection = true };
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);

        Assert.Throws<IOException>(() => keys.Generate());

        Assert.False(keys.HasKey);
        Assert.Equal([Other], files.AllFilesUnder(Folder));
        protection.RefuseProtection = false;
        using var generated = keys.Generate();
        Assert.True(keys.HasKey);
        using var publicKey = DevicePublicKey.Read(generated.PublicKeyBlob.ToArray());
        Assert.Equal(3072, publicKey.KeySize);
    }

    [Fact]
    public void A_key_file_that_cannot_be_probed_is_never_replaced_by_a_new_key()
    {
        var inner = new FakeFileSystem();
        var original = new byte[] { 1, 2, 3 };
        inner.WriteFile(KeyFile, original);
        var files = new UnprobeableFileSystem(inner, KeyFile);
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);

        Assert.ThrowsAny<Exception>(() => keys.Generate());

        Assert.Equal(0, files.Writes);
        Assert.Equal(original, inner.ReadFile(KeyFile));
    }

    [Fact]
    public void A_key_file_that_cannot_be_probed_is_reported_instead_of_being_called_missing()
    {
        var inner = new FakeFileSystem();
        inner.WriteFile(KeyFile, [1, 2, 3]);
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(new UnprobeableFileSystem(inner, KeyFile), new FakeUserProtection(), Folder);

        Assert.Throws<UnauthorizedAccessException>(() => keys.Open());
        var failure = Record.Exception(() => keys.HasKey);
        if (failure is null)
        {
            Assert.True(keys.HasKey);
        }
    }

    [Fact]
    public void A_key_that_Windows_cannot_unlock_is_reported_without_replacement()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);
        using (keys.Generate()) { }
        var before = files.ReadFile(KeyFile)!.ToArray();
        protection.RefuseOpening = true;

        Assert.ThrowsAny<Exception>(() => keys.Open());

        Assert.Equal(before, files.ReadFile(KeyFile));
    }

}
