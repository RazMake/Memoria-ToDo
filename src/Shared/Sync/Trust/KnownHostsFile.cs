using System.Text.Json;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>The user's accepted custom host keys in known-hosts.json. Unusable content is an error, never trust.</summary>
internal sealed class KnownHostsFile(IFileSystem files, string path)
{
    private const int SupportedVersion = 1;

    /// <summary>
    /// The saved trust; a missing file is empty trust. Any other read failure propagates, and content that cannot be used
    /// throws <see cref="InvalidDataException"/>, so a problem is never mistaken for an unknown host.
    /// </summary>
    public List<(string Host, int Port, string Fingerprint)> Load()
    {
        byte[] bytes;
        try
        {
            bytes = files.ReadFile(path);
        }
        catch (FileNotFoundException)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.GetProperty("version").GetInt32() != SupportedVersion)
            {
                throw new InvalidDataException("The saved host trust has an unsupported version; the file is left unchanged.");
            }

            return [.. root.GetProperty("hosts").EnumerateArray().Select(h =>
                (RequiredText(h, "host"), h.GetProperty("port").GetInt32(), RequiredText(h, "fingerprint")))];
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidDataException("The saved host trust is not usable; the file is left unchanged.", ex);
        }
    }

    private static string RequiredText(JsonElement entry, string name) =>
        entry.GetProperty(name).GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidDataException("The saved host trust has an entry without a host or fingerprint; the file is left unchanged.");

    public void Save(IEnumerable<(string Host, int Port, string Fingerprint)> hosts)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = SupportedVersion,
            hosts = hosts.Select(h => new { host = h.Host, port = h.Port, fingerprint = h.Fingerprint }),
        });
        files.WriteFile(path, content);
    }
}
