using System.Security.Cryptography;
using System.Text;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// The device's own files in the device folder, outside any clone: the saved device name and the host trust file.
/// Never the key, and never inside a repository. Call it on the owned worker only.
/// </summary>
internal sealed class DeviceMetadata(IFileSystem files, string folder, string defaultName)
{
    private string? name;

    public string DefaultName => defaultName;

    public string KnownHostsPath { get; } = Path.Combine(folder, "known-hosts.json");

    private string NamePath { get; } = Path.Combine(folder, "device-name.txt");

    /// <summary>The saved name, or the app's default when none is saved; read once, then cached.</summary>
    public string LoadName()
    {
        if (name is not null)
            return name;
        byte[] bytes;
        try
        {
            bytes = files.ReadFile(NamePath);
        }
        catch (FileNotFoundException)
        {
            return name = defaultName;
        }

        try
        {
            return name = ValidateName(Encoding.UTF8.GetString(bytes));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("The saved device name is not one non-blank line; the file is left unchanged.", ex);
        }
    }

    public void SaveName(string value)
    {
        files.WriteFile(NamePath, Encoding.UTF8.GetBytes(value));
        name = value;
    }

    /// <summary>The trimmed name, or an <see cref="ArgumentException"/> for a blank or multi-line name.</summary>
    public static string ValidateName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.AsSpan().ContainsAny('\r', '\n'))
            throw new ArgumentException("The device name must be one non-blank line.", nameof(value));
        return trimmed;
    }

    public static string PublicKeyLine(ReadOnlySpan<byte> blob, string deviceName) =>
        $"ssh-rsa {Convert.ToBase64String(blob)} {deviceName}";

    /// <summary>The OpenSSH-style SHA-256 fingerprint of a public-key blob.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> blob) =>
        "SHA256:" + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
}
