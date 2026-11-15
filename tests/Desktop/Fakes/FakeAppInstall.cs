using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IAppInstall"/>. It starts installed, at version 2.1.0, with no newer version. A test sets
/// the version the next checks find, and makes checks and downloads succeed, fail, or wait until it releases them or the
/// call is cancelled. It records the updater's starts. Tests wait on the signals, never on delays.
/// </summary>
public sealed class FakeAppInstall : IAppInstall
{
    private readonly FakeOperation<NewerVersion?> checks = new();
    private readonly FakeOperation<bool> downloads = new();
    private readonly List<(NewerVersion Version, bool Quiet)> applied = [];
    private readonly List<NewerVersion> downloaded = [];
    private readonly CountSignal applySignal = new();

    /// <inheritdoc />
    public string Version { get; set; } = "2.1.0";

    /// <inheritdoc />
    public bool IsInstalled { get; set; } = true;

    /// <summary>What a successful check finds: a newer version, or null for none. Starts null.</summary>
    public NewerVersion? Found
    {
        get => checks.Result;
        set => checks.Result = value;
    }

    /// <summary>How many checks began.</summary>
    public int CheckCount => checks.Count;

    /// <summary>How many checks ended, whether they succeeded, failed, or were cancelled.</summary>
    public int CompletedCheckCount => checks.CompletedCount;

    /// <summary>How many downloads began.</summary>
    public int DownloadCount => downloads.Count;

    /// <summary>How many downloads ended, whether they succeeded, failed, or were cancelled.</summary>
    public int CompletedDownloadCount => downloads.CompletedCount;

    /// <summary>The versions the completed downloads fetched, in order.</summary>
    public IReadOnlyList<NewerVersion> Downloaded
    {
        get { lock (downloaded) { return [.. downloaded]; } }
    }

    /// <summary>The starts of the updater, in order: the version and whether it was quiet.</summary>
    public IReadOnlyList<(NewerVersion Version, bool Quiet)> Applied
    {
        get { lock (applied) { return [.. applied]; } }
    }

    /// <summary>When set, <see cref="ApplyAfterExit"/> throws it, as when the updater can't start.</summary>
    public Exception? ApplyFailure { get; set; }

    /// <summary>From now on, checks succeed at once. This is the starting behavior.</summary>
    public void SucceedChecks() => checks.Succeed();

    /// <summary>From now on, checks fail with this exception.</summary>
    public void FailChecks(Exception exception) => checks.Fail(exception);

    /// <summary>From now on, checks wait until <see cref="ReleaseChecks"/> or until their token is cancelled.</summary>
    public void HoldChecks() => checks.Hold();

    /// <summary>Completes every held check successfully, with <see cref="Found"/>.</summary>
    public void ReleaseChecks() => checks.Release();

    /// <summary>From now on, downloads succeed at once. This is the starting behavior.</summary>
    public void SucceedDownloads() => downloads.Succeed();

    /// <summary>From now on, downloads fail with this exception.</summary>
    public void FailDownloads(Exception exception) => downloads.Fail(exception);

    /// <summary>From now on, downloads wait until <see cref="ReleaseDownloads"/> or until their token is cancelled.</summary>
    public void HoldDownloads() => downloads.Hold();

    /// <summary>Completes every held download successfully.</summary>
    public void ReleaseDownloads() => downloads.Release();

    /// <summary>Completes every held download with the failure.</summary>
    public void ReleaseDownloadsWithFailure(Exception exception) => downloads.ReleaseWithFailure(exception);

    /// <summary>Completes when at least this many checks have begun.</summary>
    public Task WaitForChecksAsync(int count) => checks.WaitForCallsAsync(count);

    /// <summary>Completes when at least this many checks have ended.</summary>
    public Task WaitForCompletedChecksAsync(int count) => checks.WaitForCompletedAsync(count);

    /// <summary>Completes when a check has seen its token cancelled.</summary>
    public Task WaitForCheckCancellationAsync() => checks.WaitForCancellationAsync();

    /// <summary>Completes when at least this many downloads have begun.</summary>
    public Task WaitForDownloadsAsync(int count) => downloads.WaitForCallsAsync(count);

    /// <summary>Completes when at least this many downloads have ended.</summary>
    public Task WaitForCompletedDownloadsAsync(int count) => downloads.WaitForCompletedAsync(count);

    /// <summary>Completes when a download has seen its token cancelled.</summary>
    public Task WaitForDownloadCancellationAsync() => downloads.WaitForCancellationAsync();

    /// <summary>Completes when at least this many starts of the updater have been recorded.</summary>
    public Task WaitForAppliesAsync(int count) => applySignal.WaitForAsync(count);

    /// <inheritdoc />
    public Task<NewerVersion?> CheckAsync(CancellationToken cancellationToken) => checks.Call(cancellationToken);

    /// <inheritdoc />
    public async Task DownloadAsync(NewerVersion version, CancellationToken cancellationToken)
    {
        await downloads.Call(cancellationToken);
        lock (downloaded) { downloaded.Add(version); }
    }

    /// <inheritdoc />
    public void ApplyAfterExit(NewerVersion version, bool quiet)
    {
        lock (applied) { applied.Add((version, quiet)); }
        applySignal.Increment();
        if (ApplyFailure is not null)
        {
            throw ApplyFailure;
        }
    }
}
