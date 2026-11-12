using System.Text.Json;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>The published SSH host fingerprints of the built-in hosts, from the embedded authoritative file.</summary>
internal sealed class HostPins
{
    private const string ResourceName = "Memoria.ToDo.Shared.Sync.HostKeys.json";
    private const int PinnedPort = 22;

    private static readonly Lazy<HostPins> Embedded = new(LoadEmbedded);

    // host name -> key family ("rsa", "ecdsa", "ed25519") -> fingerprint
    private readonly Dictionary<string, Dictionary<string, string>> byHost;

    private HostPins(Dictionary<string, Dictionary<string, string>> byHost) => this.byHost = byHost;

    public static HostPins BuiltIn => Embedded.Value;

    public bool IsBuiltIn(string host) => byHost.ContainsKey(host);

    /// <summary>True when the host has a published pin for the key's family on the pinned port that equals the fingerprint.</summary>
    public bool Matches(string host, int port, string algorithm, string fingerprint) =>
        port == PinnedPort
        && Family(algorithm) is { } family
        && byHost.TryGetValue(host, out var pins)
        && pins.TryGetValue(family, out var published)
        && string.Equals(published, fingerprint, StringComparison.Ordinal);

    private static string? Family(string algorithm) => algorithm switch
    {
        "ssh-rsa" or "rsa-sha2-256" or "rsa-sha2-512" => "rsa",
        "ssh-ed25519" => "ed25519",
        _ when algorithm.StartsWith("ecdsa-sha2-", StringComparison.Ordinal) => "ecdsa",
        _ => null,
    };

    private static HostPins LoadEmbedded()
    {
        using var stream = typeof(HostPins).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var document = JsonDocument.Parse(stream);
        var byHost = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.GetProperty("hosts").EnumerateArray())
        {
            var pins = entry.GetProperty("fingerprints").EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
            foreach (var name in entry.GetProperty("names").EnumerateArray())
            {
                byHost[name.GetString()!.ToLowerInvariant()] = pins;
            }
        }

        return new HostPins(byHost);
    }
}
