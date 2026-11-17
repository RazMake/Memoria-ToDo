using System.IO;
using System.Security.Cryptography;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class WindowsDeviceKeyStoreTests
{
    private const string Folder = @"C:\Key tests\device";
    private const string KeyFile = Folder + @"\device-key.json";

    // Real 3072-bit keys cost seconds; the real size is checked once in the integration tests.
    private const int SmallKeyBits = 1024;

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
    public void Generating_asks_for_a_3072_bit_key_and_stores_it_in_one_file()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        var requested = new List<int>();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder, bits =>
        {
            requested.Add(bits);
            return RSA.Create(SmallKeyBits);
        });

        using (keys.Generate()) { }

        Assert.Equal([3072], requested);
        Assert.True(keys.HasKey);
        Assert.Equal([KeyFile], files.AllFilesUnder(Folder));
    }

    [Fact]
    public void A_generated_key_reopens_with_the_same_public_identity_without_changing_the_file()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = SmallKeyStore(files, protection);
        byte[] publicBlob;
        using (var generated = keys.Generate())
        {
            publicBlob = generated.PublicKeyBlob.ToArray();
        }
        var before = files.ReadFile(KeyFile)!.ToArray();

        using var opened = SmallKeyStore(files, protection).Open();

        Assert.Equal(publicBlob, opened.PublicKeyBlob.ToArray());
        Assert.Equal(before, files.ReadFile(KeyFile));
    }

    [Theory]
    [InlineData(RsaSignatureAlgorithm.Sha256)]
    [InlineData(RsaSignatureAlgorithm.Sha512)]
    public void A_reopened_key_signs_with_RSA_SHA2(RsaSignatureAlgorithm algorithm)
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        using (SmallKeyStore(files, protection).Generate()) { }
        using var opened = SmallKeyStore(files, protection).Open();
        var data = Encoding.UTF8.GetBytes("A device-authentication challenge");

        var signature = opened.Sign(data, algorithm);

        using var publicKey = DevicePublicKey.Read(opened.PublicKeyBlob.ToArray());
        Assert.True(publicKey.VerifyData(data, signature,
            algorithm == RsaSignatureAlgorithm.Sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512,
            RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Generating_and_opening_clear_the_passphrase_buffers_they_used()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        using (SmallKeyStore(files, protection).Generate()) { }
        Assert.NotEmpty(protection.Input);
        Assert.All(protection.Input, value => Assert.Equal(0, value));

        using var opened = SmallKeyStore(files, protection).Open();

        Assert.NotEmpty(protection.Unprotected);
        Assert.All(protection.Unprotected, value => Assert.Equal(0, value));
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
        IDeviceKeyStore keys = SmallKeyStore(files, protection);

        Assert.Throws<IOException>(() => keys.Generate());

        Assert.False(keys.HasKey);
        Assert.Equal([Other], files.AllFilesUnder(Folder));
        protection.RefuseProtection = false;
        using var generated = keys.Generate();
        Assert.True(keys.HasKey);
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
        Assert.Throws<UnauthorizedAccessException>(() => keys.HasKey);
    }

    [Fact]
    public void A_key_that_Windows_cannot_unlock_is_reported_without_replacement()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = SmallKeyStore(files, protection);
        using (keys.Generate()) { }
        var before = files.ReadFile(KeyFile)!.ToArray();
        protection.RefuseOpening = true;

        Assert.ThrowsAny<Exception>(() => keys.Open());

        Assert.Equal(before, files.ReadFile(KeyFile));
    }

    [Fact]
    public void Deleting_removes_the_key_file_and_only_that_file()
    {
        const string Other = Folder + @"\device-name.txt";
        var files = new FakeFileSystem();
        files.WriteFile(Other, [1]);
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = SmallKeyStore(files, protection);
        using (keys.Generate()) { }

        keys.Delete();

        Assert.False(keys.HasKey);
        Assert.False(files.FileExists(KeyFile));
        Assert.Equal([Other], files.AllFilesUnder(Folder));
    }

    [Fact]
    public void Deleting_with_no_key_does_nothing_and_does_not_fail()
    {
        const string Other = Folder + @"\device-name.txt";
        var files = new FakeFileSystem();
        files.WriteFile(Other, [1]);
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);

        keys.Delete();

        Assert.False(keys.HasKey);
        Assert.Equal([Other], files.AllFilesUnder(Folder));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{\"version\":2,\"encryptedPrivateKey\":\"AA==\",\"protectedPassphrase\":\"AA==\"}")]
    public void Deleting_removes_a_stored_key_that_cannot_be_opened(string contents)
    {
        var files = new FakeFileSystem();
        files.WriteFile(KeyFile, Encoding.UTF8.GetBytes(contents));
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = new WindowsDeviceKeyStore(files, protection, Folder);
        Assert.True(keys.HasKey);

        keys.Delete();

        Assert.False(keys.HasKey);
        Assert.Empty(files.AllFilesUnder(Folder));
    }

    [Fact]
    public void Deleting_removes_a_key_that_Windows_can_no_longer_unlock_without_trying_to_unlock_it()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = SmallKeyStore(files, protection);
        using (keys.Generate()) { }
        protection.RefuseOpening = true;

        keys.Delete();

        Assert.False(keys.HasKey);
        Assert.Empty(protection.Unprotected);
    }

    [Fact]
    public void A_failed_deletion_reports_the_error_and_keeps_the_key_file_whole()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = SmallKeyStore(files, protection);
        using (keys.Generate()) { }
        var before = files.ReadFile(KeyFile).ToArray();
        files.FailDeletesUnder(Folder);

        Assert.ThrowsAny<IOException>(() => keys.Delete());

        Assert.True(keys.HasKey);
        Assert.Equal(before, files.ReadFile(KeyFile));
    }

    [Fact]
    public void A_key_generated_after_deleting_is_a_new_key_that_reopens()
    {
        var files = new FakeFileSystem();
        using var protection = new FakeUserProtection();
        IDeviceKeyStore keys = SmallKeyStore(files, protection);
        byte[] oldPublicKey;
        using (var first = keys.Generate())
        {
            oldPublicKey = first.PublicKeyBlob.ToArray();
        }

        keys.Delete();
        byte[] newPublicKey;
        using (var second = keys.Generate())
        {
            newPublicKey = second.PublicKeyBlob.ToArray();
        }

        Assert.NotEqual(oldPublicKey, newPublicKey);
        Assert.True(keys.HasKey);
        using var reopened = SmallKeyStore(files, protection).Open();
        Assert.Equal(newPublicKey, reopened.PublicKeyBlob.ToArray());
        Assert.Equal([KeyFile], files.AllFilesUnder(Folder));
    }

    private static WindowsDeviceKeyStore SmallKeyStore(IFileSystem files, IUserProtection protection) =>
        new(files, protection, Folder, _ => RSA.Create(SmallKeyBits));
}
