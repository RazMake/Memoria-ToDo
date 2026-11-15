namespace Memoria.ToDo.Shared.Sync;

/// <summary>Whether to trust a host's key, and the failure to report when not.</summary>
internal sealed record HostKeyOutcome(bool CanTrust, GitFailureException? Failure);

/// <summary>Turns the verdict on a host's key into the connection decision and the failure a sync reports.</summary>
internal static class HostKeyCheck
{
    public static HostKeyOutcome Run(string host, Func<HostKeyVerdict> verify)
    {
        HostKeyVerdict verdict;
        try
        {
            verdict = verify();
        }
        catch (Exception ex)
        {
            // Not reading the saved trust is a fault of this device or of the code, never the host's or the network's.
            // A requested cancellation still wins: the operation's cancelled token turns this failure into a cancellation.
            return new HostKeyOutcome(false, FailureReport.ToLocalFailure(ex));
        }

        return verdict switch
        {
            HostKeyVerdict.Trusted => new HostKeyOutcome(true, null),
            HostKeyVerdict.Unknown => new HostKeyOutcome(false, new GitFailureException(SyncFailure.UnknownHostKey, $"{host} presented a host key that isn't trusted yet.")),
            HostKeyVerdict.Changed => new HostKeyOutcome(false, new GitFailureException(SyncFailure.ChangedHostKey, $"{host} presented a different host key than the one trusted.")),
            _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, null),
        };
    }
}
