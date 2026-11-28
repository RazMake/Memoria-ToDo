namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// What the running native call may use. libgit2 creates its SSH transport without any context, so the worker thread
/// publishes the operation here for the length of one native call, and clears it afterwards.
/// </summary>
internal sealed class OperationScope : IDisposable
{
    [ThreadStatic]
    private static OperationScope? current;

    // The failure and how it arose are published together by one reference write, so they can't be seen apart.
    private sealed record CapturedFailure(GitFailureException Failure, bool WrappedFromPlainException);

    private CapturedFailure? captured;

    private OperationScope(GitConnection connection, CancellationToken token, TimeProvider time)
    {
        Connection = connection;
        Token = token;
        Time = time;
    }

    /// <summary>The scope of the native call running on this thread; throws when there is none.</summary>
    public static OperationScope Current =>
        current ?? throw new InvalidOperationException("The SSH transport was used outside a Sync operation.");

    public GitConnection Connection { get; }

    public CancellationToken Token { get; }

    public TimeProvider Time { get; }

    /// <summary>The failure Sync classified inside the transport; libgit2 keeps only its message, so it is kept here.</summary>
    public GitFailureException? Failure => Volatile.Read(ref captured)?.Failure;

    /// <summary>
    /// Keeps the first transport failure, classifying a plain I/O error as a connection failure. That guess is
    /// replaced only by the host's own explanation (a repository or authentication failure Sync classified).
    /// </summary>
    public void Record(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return;
        }

        var incoming = ex as GitFailureException;
        var next = new CapturedFailure(incoming ?? new GitFailureException(SyncFailure.Connection, ex.Message, ex), incoming is null);
        var explained = incoming is { Kind: SyncFailure.Repository or SyncFailure.Authentication };
        // Callbacks, cleanup and the disconnect on cancel can record at once; the first one wins, except over a guess.
        for (var current = Volatile.Read(ref captured); current is null || (explained && current.WrappedFromPlainException);)
        {
            var seen = Interlocked.CompareExchange(ref captured, next, current);
            if (ReferenceEquals(seen, current))
            {
                return;
            }

            current = seen;
        }
    }

    public static OperationScope Enter(GitConnection connection, CancellationToken token, TimeProvider time)
    {
        if (current is not null)
        {
            throw new InvalidOperationException("A Sync operation is already running on this thread.");
        }

        return current = new OperationScope(connection, token, time);
    }

    public void Dispose() => current = null;
}
