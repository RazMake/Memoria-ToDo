using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The device key as one local file: the RSA private key encrypted with a random passphrase, and that passphrase
/// protected for the current Windows account. See docs/architecture/sync.md. A key that can't be opened is reported,
/// never replaced. Call it on Sync's worker only.
/// </summary>
internal sealed class WindowsDeviceKeyStore : IDeviceKeyStore
{
    private const int KeyBits = 3072;
    private const int PassphraseBytes = 32;
    private const int Iterations = 100_000;
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IFileSystem _files;
    private readonly IUserProtection _protection;
    private readonly string _path;

    public WindowsDeviceKeyStore(IFileSystem files, IUserProtection protection, string deviceFolder)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFolder);
        _files = files;
        _protection = protection;
        _path = Path.Combine(deviceFolder, "device-key.json");
    }

    public bool HasKey => ReadIfPresent() is not null;

    public IDeviceSigningKey Generate()
    {
        if (HasKey)
        {
            throw new InvalidOperationException("This device already has a key.");
        }

        var passphrase = RandomNumberGenerator.GetBytes(PassphraseBytes);
        var rsa = RSA.Create(KeyBits);
        try
        {
            var encrypted = rsa.ExportEncryptedPkcs8PrivateKey(
                passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, Iterations));
            var envelope = new Envelope(CurrentVersion, Convert.ToBase64String(encrypted), Convert.ToBase64String(_protection.Protect(passphrase)));
            _files.WriteFile(_path, JsonSerializer.SerializeToUtf8Bytes(envelope, Json));
            return new RsaSigningKey(rsa);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passphrase);
        }
    }

    public IDeviceSigningKey Open()
    {
        var contents = ReadIfPresent() ?? throw new FileNotFoundException("This device has no key yet.", _path);
        var envelope = ReadEnvelope(contents);
        byte[]? passphrase = null;
        var rsa = RSA.Create();
        try
        {
            passphrase = _protection.Unprotect(Convert.FromBase64String(envelope.ProtectedPassphrase!));
            rsa.ImportEncryptedPkcs8PrivateKey(passphrase, Convert.FromBase64String(envelope.EncryptedPrivateKey!), out _);
            return new RsaSigningKey(rsa);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
        finally
        {
            if (passphrase is not null)
            {
                CryptographicOperations.ZeroMemory(passphrase);
            }
        }
    }

    // The strict read tells a missing file from one that can't be read; a file that can't be probed is never "no key".
    private byte[]? ReadIfPresent()
    {
        try
        {
            return _files.ReadFile(_path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private Envelope ReadEnvelope(byte[] contents)
    {
        Envelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(contents, Json);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"The device key file {_path} isn't readable.", error);
        }

        if (envelope is not { Version: CurrentVersion, EncryptedPrivateKey: not null, ProtectedPassphrase: not null })
        {
            throw new InvalidDataException($"The device key file {_path} isn't a version {CurrentVersion} key.");
        }

        return envelope;
    }

    private sealed record Envelope(int Version, string? EncryptedPrivateKey, string? ProtectedPassphrase);
}
