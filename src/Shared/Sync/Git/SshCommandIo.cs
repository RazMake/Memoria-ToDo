namespace Memoria.ToDo.Shared.Sync;

/// <summary>One running remote command: blocking reads and writes for libgit2's synchronous callbacks, and an async end.</summary>
internal sealed class SshCommandIo
{
    /// <summary>How long a command gets to exit by itself once its input ended, apart from the network inactivity limit.</summary>
    private static readonly TimeSpan GracefulExit = TimeSpan.FromSeconds(5);

    // The finite phrases hosts use to refuse a key that can't write, or a key outright.
    private static readonly string[] Refusals =
    [
        "permission denied",
        "access denied",
        "marked as read only",
        "marked as read-only",
        "does not have write access",
    ];

    private readonly IRemoteCommand command;
    private readonly OperationScope scope;
    private readonly TimeProvider time;
    private readonly InactivityWatchdog watchdog;
    private readonly TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration cancellation;
    private volatile Task interrupt = Task.CompletedTask;
    private Task execution = Task.CompletedTask;
    private Stream input = Stream.Null;
    private bool started;

    /// <summary>
    /// Registers the cancel callback, so a cancel can end even the channel open that <see cref="Begin"/> blocks on.
    /// Nothing else is started: call <see cref="Begin"/>, then <see cref="EndAsync"/> whether or not it succeeded.
    /// </summary>
    /// <param name="disconnect">Drops the connection to end a blocked read; it must not throw.</param>
    public SshCommandIo(IRemoteCommand command, CancellationToken token, Action disconnect, InactivityWatchdog watchdog, OperationScope scope)
    {
        this.command = command;
        this.scope = scope;
        time = scope.Time;
        this.watchdog = watchdog;
        Token = token;

        // The callback runs on whoever cancels, maybe a UI thread, so it only queues the disconnect, which EndAsync awaits.
        cancellation = token.Register(() => interrupt = Task.Run(() =>
        {
            try
            {
                Unblock();
                disconnect();
            }
            finally
            {
                cancelled.TrySetResult();
            }
        }, CancellationToken.None));
    }

    /// <summary>Opens the channel and starts the command. Blocks until the host answers, or a cancel disconnects.</summary>
    public void Begin()
    {
        execution = command.ExecuteAsync();
        try
        {
            input = command.CreateInputStream();
        }
        catch (InvalidOperationException)
        {
            // The host already ended the command, maybe after writing its refusal. Input goes nowhere, as on any closed
            // channel, and EndAsync judges the exit report as when the command ends later.
        }

        started = true;
    }

    private CancellationToken Token { get; }

    /// <summary>
    /// Ends a blocked <see cref="Read"/>. SSH.NET's output stream waits on its own monitor, which a disconnect doesn't
    /// pulse, so the stream is disposed too. Never throws.
    /// </summary>
    public void Unblock()
    {
        try
        {
            command.OutputStream.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed with the command; the read is over either way.
        }
        catch (Exception ex)
        {
            // Any other failure is recorded, after the cause that made us unblock, and the caller carries on interrupting.
            scope.Record(ex);
        }
    }

    // Only the fault our own dispose causes, or a caller's cancel, is expected; any other failure of the task is recorded.
    private static async Task ObserveAbandoned(Task execution, OperationScope scope)
    {
        try
        {
            await execution.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException || (ex is OperationCanceledException && scope.Token.IsCancellationRequested))
        {
        }
        catch (Exception ex)
        {
            scope.Record(ex);
        }
    }

    /// <summary>
    /// Reads up to <paramref name="count"/> bytes. Zero means the output ended; why the command ended is reported by
    /// <see cref="EndAsync"/>, because the exit status can arrive after the output.
    /// </summary>
    public int Read(byte[] buffer, int count)
    {
        int read;
        watchdog.Begin();
        try
        {
            // Begin does nothing once the watchdog expired, and the one-shot cut-off may have missed this command, so don't block.
            if (watchdog.Expired)
            {
                throw TimedOut();
            }

            read = command.OutputStream.Read(buffer, 0, count);
        }
        catch (Exception) when (watchdog.Expired)
        {
            throw TimedOut();
        }
        catch (Exception) when (Token.IsCancellationRequested)
        {
            throw new OperationCanceledException(Token);
        }
        finally
        {
            watchdog.End();
        }

        if (read > 0)
        {
            return read;
        }

        Token.ThrowIfCancellationRequested();
        return watchdog.Expired ? throw TimedOut() : 0;
    }

    public void Write(byte[] buffer, int count)
    {
        watchdog.Begin();
        try
        {
            input.Write(buffer, 0, count);
            input.Flush();
        }
        catch (Exception) when (watchdog.Expired)
        {
            throw TimedOut();
        }
        finally
        {
            watchdog.End();
        }
    }

    /// <summary>Sends EOF, as git with OpenSSH does, so the server ends the command by itself.</summary>
    public void SendEof()
    {
        watchdog.Begin();
        try
        {
            input.Dispose();
        }
        catch (Exception) when (watchdog.Expired)
        {
            throw TimedOut();
        }
        finally
        {
            watchdog.End();
        }
    }

    /// <summary>
    /// Sends EOF, waits for the server to end the command, then releases it. The command gets five seconds to exit
    /// after its input ended, and the wait also ends after the network timeout of no activity. A fault while sending EOF
    /// doesn't skip the wait: the exit report decides, so a host's refusal is never reported as a connection loss.
    /// When the wait is cut short, or a caller cancels, disposing the SSH.NET command
    /// completes its execution task, and this method awaits that task before it returns, so no work outlives it.
    /// Returns why the command failed, or null when it ended well or was cancelled.
    /// </summary>
    public async Task<GitFailureException?> EndAsync()
    {
        if (!started)
        {
            return await EndUnstartedAsync().ConfigureAwait(false);
        }

        GitFailureException? failure = null;
        var cutShort = false;
        var graceful = false;
        using var graceTimer = new CancellationTokenSource();
        try
        {
            try
            {
                SendEof();
            }
            catch (Exception) when (!Token.IsCancellationRequested && !watchdog.Expired)
            {
                // A host that refused the command may close the channel before EOF; its exit report, awaited below, says why.
            }

            watchdog.Begin();
            var grace = Task.Delay(GracefulExit, time, graceTimer.Token);
            var ended = await Task.WhenAny(execution, watchdog.ExpiredTask, cancelled.Task, grace).ConfigureAwait(false);
            cutShort = ended != execution;
            graceful = ended == grace;
        }
        catch (Exception ex)
        {
            failure = FailureOf(ex);
            cutShort = true;
        }
        finally
        {
            watchdog.End();
            await graceTimer.CancelAsync().ConfigureAwait(false);
        }

        if (cutShort)
        {
            command.Dispose();
            failure ??= graceful && !Token.IsCancellationRequested ? NotExited()
                : watchdog.Expired && !Token.IsCancellationRequested ? TimedOut() : null;
        }

        try
        {
            await execution.ConfigureAwait(false);
            failure ??= cutShort ? null : ExitFailure();
        }
        catch (Exception ex)
        {
            failure ??= cutShort ? null : FailureOf(ex);
        }
        finally
        {
            await ReleaseAsync().ConfigureAwait(false);
        }

        return failure;
    }

    // A command whose Begin failed or never ran: its failure was already thrown to the caller. Only an execution fault
    // that isn't the dispose or a cancel is recorded, and the cancel work is joined as for any command.
    private async Task<GitFailureException?> EndUnstartedAsync()
    {
        try
        {
            command.Dispose();
            await ObserveAbandoned(execution, scope).ConfigureAwait(false);
        }
        finally
        {
            await ReleaseAsync().ConfigureAwait(false);
        }

        return null;
    }

    // No cancel callback can start after the registration is disposed, so the interrupt it queued is the last, and is awaited.
    private async Task ReleaseAsync()
    {
        await cancellation.DisposeAsync().ConfigureAwait(false);
        await interrupt.ConfigureAwait(false);
        command.Dispose();
    }

    // Null when the caller cancelled: it already knows why.
    private GitFailureException? FailureOf(Exception ex) => ex switch
    {
        _ when Token.IsCancellationRequested => null,
        GitFailureException failure => failure,
        _ when watchdog.Expired => TimedOut(),
        _ => new GitFailureException(SyncFailure.Connection, $"'{command.CommandText}' failed: {ex.Message}", ex),
    };

    private GitFailureException NotExited() =>
        new(SyncFailure.Connection, $"'{command.CommandText}' didn't exit within {GracefulExit.TotalSeconds:0} seconds of its input ending.");

    private GitFailureException TimedOut() =>
        new(SyncFailure.Connection, $"'{command.CommandText}' got no data from the host for the whole network timeout.");

    // Only the server's exit status 0 is success. Explicit refusals of the key or of writing mean Authentication;
    // any other ending, including no status at all, is a Repository failure that keeps what the server said.
    private GitFailureException? ExitFailure()
    {
        var status = command.ExitStatus;
        if (status == 0)
        {
            return null;
        }

        var error = command.Error.Trim();
        if (status is null)
        {
            var signal = string.IsNullOrEmpty(command.ExitSignal) ? "no exit signal" : $"exit signal {command.ExitSignal}";
            return new GitFailureException(SyncFailure.Repository, $"'{command.CommandText}' ended without an exit status ({signal}): {error}");
        }

        var kind = Refusals.Any(phrase => error.Contains(phrase, StringComparison.OrdinalIgnoreCase)) ? SyncFailure.Authentication : SyncFailure.Repository;
        return new GitFailureException(kind, $"'{command.CommandText}' exited with {status}: {error}");
    }
}
