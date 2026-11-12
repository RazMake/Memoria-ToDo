namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Proves the device key may push: a host that accepts the key for reading only refuses <c>git-receive-pack</c>, which
/// the command's exit status and error text show once it ends. This avoids a first sync that fails only after the user
/// has added a read-only key.
/// </summary>
internal static class WriteAccessCheck
{
    /// <summary>Checks inside the running operation, whose scope supplies the connection, token and clock.</summary>
    public static void Run(SshAddress address)
    {
        var scope = OperationScope.Current;
        var link = SshLink.Open(address, scope);
        var advertised = false;
        try
        {
            var io = link.Start("git-receive-pack", address.Path, scope.Token);
            if (io.Read(new byte[4096], 4096) > 0)
            {
                advertised = true;
                // An empty request (flush-pkt) tells receive-pack there is nothing to push; plain EOF makes it exit 128.
                io.Write("0000"u8.ToArray(), 4);
            }

            // Empty output proves nothing by itself: why the command ended is the exit status, judged by the awaited cleanup.
            scope.Token.ThrowIfCancellationRequested();
            io.SendEof();
        }
        catch (Exception ex)
        {
            scope.Record(ex);
            throw;
        }
        finally
        {
            scope.Connection.Cleanup.Add(CloseThenJudge(link, scope, advertised));
        }
    }

    // A command that exits 0 yet advertised nothing isn't a receive-pack that accepted the key. Its own exit failure,
    // recorded while closing, comes first; this only fills in when there was none.
    private static async Task CloseThenJudge(SshLink link, OperationScope scope, bool advertised)
    {
        await link.CloseAsync().ConfigureAwait(false);
        if (!advertised)
        {
            scope.Record(new GitFailureException(SyncFailure.Repository,
                "git-receive-pack advertised nothing, so write access couldn't be verified."));
        }
    }
}
