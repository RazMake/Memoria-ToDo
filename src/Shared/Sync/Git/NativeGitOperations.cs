using LibGit2Sharp;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>The LibGit2Sharp and SSH.NET implementation of <see cref="IGitOperations"/>. Its native calls run on the caller's worker thread.</summary>
internal sealed class NativeGitOperations(ITaskSynchronization tasks, TimeProvider time, OwnedWorker worker) : IGitOperations
{
    private const string SyncedKey = "memoria.lastSuccessfulSync";
    private const string FingerprintKey = "memoria.deviceKeyFingerprint";

    public GitReceipt? ReadReceipt(string folder)
    {
        if (!Repository.IsValid(folder))
        {
            return null;
        }

        using var repo = new Repository(folder);
        var synced = repo.Config.Get<string>(SyncedKey, ConfigurationLevel.Local)?.Value;
        var fingerprint = repo.Config.Get<string>(FingerprintKey, ConfigurationLevel.Local)?.Value;
        return synced is not null && fingerprint is not null
            && DateTimeOffset.TryParseExact(synced, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var at)
                ? new GitReceipt(at, fingerprint)
                : null;
    }

    public void WriteReceipt(string folder, GitReceipt receipt)
    {
        using var repo = new Repository(folder);
        repo.Config.Set(SyncedKey, receipt.LastSuccessfulSync.ToString("O", System.Globalization.CultureInfo.InvariantCulture), ConfigurationLevel.Local);
        repo.Config.Set(FingerprintKey, receipt.KeyFingerprint, ConfigurationLevel.Local);
    }

    public string? ReadOrigin(string folder)
    {
        if (!Repository.IsValid(folder))
        {
            return null;
        }

        using var repo = new Repository(folder);
        return repo.Network.Remotes["origin"]?.Url;
    }

    public void UseExisting(string folder, SshAddress address)
    {
        if (!Repository.IsValid(folder))
        {
            throw new GitFailureException(SyncFailure.Repository, "The folder is not a Git repository.");
        }

        using var repo = new Repository(folder);
        if (repo.Info.IsBare || !SamePath(repo.Info.WorkingDirectory, folder))
        {
            throw new GitFailureException(SyncFailure.Repository, "The folder is not the top of a Git working copy.");
        }

        SafeRepositoryState.Require(repo);
        var hadOrigin = repo.Network.Remotes["origin"] is not null;
        if (OriginRemote.Ensure(repo, address.Text).Changed && hadOrigin)
        {
            repo.Config.Unset(SyncedKey, ConfigurationLevel.Local);
            repo.Config.Unset(FingerprintKey, ConfigurationLevel.Local);
        }
    }

    public Task Clone(GitConnection connection, SshAddress address, string folder, CancellationToken cancellationToken)
    {
        // Only a clone that started owns the folder's contents; a refused non-empty folder is never touched.
        var started = false;
        var created = false;
        return Run(connection, cancellationToken, () =>
        {
            created = !Directory.Exists(folder);
            if (!created && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                throw new GitFailureException(SyncFailure.Repository, "The folder for the clone isn't empty.");
            }

            started = true;
            Repository.Clone(address.Text, folder, new CloneOptions
            {
                FetchOptions = { OnTransferProgress = _ => !cancellationToken.IsCancellationRequested },
            });
            WriteAccessCheck.Run(address);
            return true;
        }, () =>
        {
            if (started)
            {
                FolderCleaner.Clear(folder, created);
            }
        });
    }

    public async Task<GitPassResult> RunPass(GitConnection connection, string folder, CancellationToken cancellationToken)
    {
        var pass = new GitPass(tasks, time, folder, connection, cancellationToken);
        try
        {
            return await Run(connection, cancellationToken, pass.Run).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && pass.PendingIfFailed is { } pending)
        {
            throw new PassFailedException(ex, pending);
        }
    }

    // The operation ends only after its transport cleanup, so a command's exit status, which arrives after its output,
    // can still fail an operation whose body returned normally. The scope is cleared before the first await, on the
    // worker thread that set it. onFailure undoes the operation's changes once its failure is known.
    private async Task<T> Run<T>(GitConnection connection, CancellationToken token, Func<T> body, Action? onFailure = null)
    {
        SshRegistration.Ensure();
        var scope = OperationScope.Enter(connection, token, time);
        T result = default!;
        Exception? error = null;
        try
        {
            result = body();
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            scope.Dispose();
        }

        await connection.Cleanup.WhenAllAsync().ConfigureAwait(false);
        // A caller cancel that arrives while the transport closes still ends the operation as cancelled, never as success.
        Exception? failure = error is not null ? Translate(error, scope, token)
            : token.IsCancellationRequested ? new OperationCanceledException(token)
            : scope.Failure;
        if (failure is null)
        {
            return result;
        }

        if (onFailure is not null)
        {
            // Local file work belongs on the owned worker, not on whichever thread the awaits resumed on.
            try
            {
                await worker.Run(onFailure).ConfigureAwait(false);
            }
            catch (Exception undo) when (failure is GitFailureException git)
            {
                // The undo failing must not hide why the operation failed: same kind, and both reasons in the details.
                throw new GitFailureException(git.Kind, $"{git.Message} Undoing the changes also failed: {undo.Message}", new AggregateException(git, undo));
            }
            catch (Exception undo) when (failure is OperationCanceledException && undo is IOException or UnauthorizedAccessException)
            {
                // A plain cancel stays a cancel, but one whose undo failed left files behind, which the user must hear about.
                throw new IOException($"The operation was cancelled, but undoing its changes failed, so some files may remain in the folder: {undo.Message}",
                    new AggregateException(failure, undo));
            }
        }

        throw failure;
    }

    private static Exception Translate(Exception ex, OperationScope scope, CancellationToken token) => ex switch
    {
        _ when token.IsCancellationRequested => new OperationCanceledException(token),
        GitFailureException or OperationCanceledException => ex,
        _ when scope.Failure is { } failure => failure,
        LibGit2SharpException => new GitFailureException(SyncFailure.Repository, ex.Message, ex),
        IOException or UnauthorizedAccessException => new GitFailureException(SyncFailure.LocalStorage, ex.Message, ex),
        _ => ex,
    };

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
