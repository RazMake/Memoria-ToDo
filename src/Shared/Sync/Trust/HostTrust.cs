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

    /// <summary>Saves trust for the currently offered unknown key. Throws InvalidOperationException for anything else.</summary>
    public void Accept(string fingerprint)
    {
        SshHostKey key;
        lock (gate)
        {
            key = offered is { CanAccept: true } candidate && candidate.Fingerprint == fingerprint
                ? candidate
                : throw new InvalidOperationException("That host key is not currently offered for acceptance.");
        }

        var hosts = saved.Load();
        hosts.Add((key.Host, key.Port, key.Fingerprint));
        saved.Save(hosts);
        lock (gate)
        {
            offered = null;
        }

        offeredChanged();
    }

    /// <summary>Removes saved trust for a custom host. A built-in host's key is fixed, so forgetting it throws InvalidOperationException.</summary>
    public void Forget(string host, int port)
    {
        host = AddressParser.NormalizeHost(host);
        if (pins.IsBuiltIn(host))
        {
            throw new InvalidOperationException("A built-in host's key is fixed and cannot be forgotten.");
        }

        var hosts = saved.Load();
        if (hosts.RemoveAll(h => h.Host == host && h.Port == port) > 0)
        {
            saved.Save(hosts);
        }
    }

    private HostKeyVerdict Decide(string host, int port, string algorithm, string fingerprint, out bool canAccept)
    {
        canAccept = false;
        if (pins.IsBuiltIn(host))
        {
            return pins.Matches(host, port, algorithm, fingerprint) ? HostKeyVerdict.Trusted : HostKeyVerdict.Changed;
        }

        var hosts = saved.Load();
        var known = hosts.Where(h => h.Host == host && h.Port == port).ToList();
        if (known.Count == 0)
        {
            canAccept = true;
            return HostKeyVerdict.Unknown;
        }

        return known.Any(h => h.Fingerprint == fingerprint) ? HostKeyVerdict.Trusted : HostKeyVerdict.Changed;
    }

    private sealed record SshHostKey(string Host, int Port, string Algorithm, string Fingerprint, bool CanAccept) : ISshHostKey;
}
