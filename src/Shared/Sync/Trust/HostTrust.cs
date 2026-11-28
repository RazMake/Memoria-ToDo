namespace Memoria.ToDo.Shared.Sync;

/// <summary>Decides whether an offered SSH host key is trusted, and records the latest offered key for the user.</summary>
internal sealed class HostTrust(HostPins pins, KnownHostsFile saved, Action offeredChanged)
{
    private readonly object gate = new();
    private SshHostKey? offered;

    public ISshHostKey? Offered
    {
        get { lock (gate) { return offered; } }
    }

    /// <summary>Thread-safe: Git calls it from its transport while a pass or clone runs.</summary>
    public HostKeyVerdict Verify(string host, int port, string algorithm, string fingerprint)
    {
        HostKeyVerdict verdict;
        var canAccept = false;
        try
        {
            verdict = Decide(host, port, algorithm, fingerprint, out canAccept);
        }
        finally
        {
            lock (gate)
            {
                offered = new SshHostKey(host, port, algorithm, fingerprint, canAccept);
            }

            offeredChanged();
        }

        return verdict;
    }

    /// <summary>Saves trust for the currently offered unknown or changed key, replacing every saved key of its host and port. Throws InvalidOperationException for anything else.</summary>
    public void Accept(string fingerprint)
    {
        SshHostKey key;
        lock (gate)
        {
            key = offered is { CanAccept: true } candidate && candidate.Fingerprint == fingerprint
                ? candidate
                : throw new InvalidOperationException("That host key is not currently offered for acceptance.");
        }

        var hosts = LoadSaved();
        var host = AddressParser.NormalizeHost(key.Host);
        hosts.RemoveAll(h => AddressParser.NormalizeHost(h.Host) == host && h.Port == key.Port);
        hosts.Add((host, key.Port, key.Fingerprint));
        saved.Save(hosts);
        lock (gate)
        {
            offered = null;
        }

        offeredChanged();
    }

    // The one place the saved trust is loaded. While it can't be, the offered key stays visible but can't be accepted.
    private List<(string Host, int Port, string Fingerprint)> LoadSaved()
    {
        try
        {
            return saved.Load();
        }
        catch
        {
            lock (gate)
            {
                offered = offered is { } key ? new SshHostKey(key.Host, key.Port, key.Algorithm, key.Fingerprint, false) : null;
            }

            throw;
        }
    }

    private HostKeyVerdict Decide(string host, int port, string algorithm, string fingerprint, out bool canAccept)
    {
        canAccept = false;
        if (pins.IsBuiltIn(host))
        {
            return pins.Matches(host, port, algorithm, fingerprint) ? HostKeyVerdict.Trusted : HostKeyVerdict.Changed;
        }

        var hosts = LoadSaved();
        var normalized = AddressParser.NormalizeHost(host);
        var known = hosts.Where(h => AddressParser.NormalizeHost(h.Host) == normalized && h.Port == port).ToList();
        if (known.Any(h => h.Fingerprint == fingerprint))
        {
            return HostKeyVerdict.Trusted;
        }

        canAccept = true;
        return known.Count == 0 ? HostKeyVerdict.Unknown : HostKeyVerdict.Changed;
    }

    private sealed record SshHostKey(string Host, int Port, string Algorithm, string Fingerprint, bool CanAccept) : ISshHostKey;
}
