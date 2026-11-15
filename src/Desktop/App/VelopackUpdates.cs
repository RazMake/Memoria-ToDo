using System.Collections.Concurrent;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Updates;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Updates' install port over Velopack and the GitHub Releases of the repository. A check keeps the release feed it
/// read, so it can give the notes of every release after the current one, not only the newest; a download or an apply
/// works on the release that check found.
/// </summary>
internal sealed class VelopackUpdates : IAppInstall
{
    private readonly UpdateManager _manager;
    private readonly FeedRecorder _source;
    private readonly ConcurrentDictionary<string, UpdateInfo> _found = new();
    private readonly Uri _releasesPage;

    public VelopackUpdates(string repositoryUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        _releasesPage = new Uri(repositoryUrl.TrimEnd('/') + "/releases");
        _source = new FeedRecorder(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));
        _manager = new UpdateManager(_source);
    }

    public bool IsInstalled => _manager.IsInstalled;

    public string Version => WithoutMetadata(_manager.IsInstalled && _manager.CurrentVersion is { } current
        ? current.ToString()
        : AppVersion.Value);

    public async Task<NewerVersion?> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_manager.IsInstalled)
        {
            return null;
        }

        // The check has no cancellation token, so a cancel stops the wait for it.
        var update = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (update is null)
        {
            return null;
        }

        var newest = update.TargetFullRelease;
        var current = _manager.CurrentVersion;
        var releases = _source.Releases;
        var notes = releases
            .Where(release => release.Version <= newest.Version && (current is null || release.Version > current))
            .OrderByDescending(release => release.Version)
            .Select(release => new ReleaseNotes(WithoutMetadata(release.Version.ToString()),
                string.IsNullOrEmpty(release.NotesMarkdown) ? null : release.NotesMarkdown))
            .ToList();
        var version = WithoutMetadata(newest.Version.ToString());
        _found[version] = update;
        var olderNotesPage = current is not null && FeedStopsAfter(releases.Select(release => release.Version), current) ? _releasesPage : null;
        return new NewerVersion(version, notes, olderNotesPage);
    }

    /// <summary>
    /// Whether a release feed may leave out releases after <paramref name="current"/>: it holds no release at or older than the
    /// running version. Velopack reads GitHub a page at a time before it drops prereleases, so a short feed doesn't prove it is complete.
    /// </summary>
    internal static bool FeedStopsAfter<TVersion>(IEnumerable<TVersion> feed, TVersion current)
        where TVersion : IComparable<TVersion> =>
        !feed.Any(version => version.CompareTo(current) <= 0);

    public async Task DownloadAsync(NewerVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_manager.IsInstalled)
        {
            return;
        }

        await _manager.DownloadUpdatesAsync(Found(version), cancelToken: cancellationToken).ConfigureAwait(false);
    }

    public void ApplyAfterExit(NewerVersion version, bool quiet)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!_manager.IsInstalled)
        {
            return;
        }

        // A start at sign-in shows no window, and the quiet update brings the app back the same way.
        _manager.WaitExitThenApplyUpdates(Found(version).TargetFullRelease, silent: quiet, restart: true,
            restartArgs: quiet ? [StartArguments.AtSignIn] : []);
    }

    // Each version keeps the check result that found it, so a later check can't change what an update downloads or applies.
    private UpdateInfo Found(NewerVersion version) =>
        _found.TryGetValue(version.Version, out var found)
            ? found
            : throw new InvalidOperationException($"No check found version {version.Version}.");

    private static string WithoutMetadata(string version) => version.Split('+')[0];

    // Remembers the full releases of the last feed read, with their notes.
    private sealed class FeedRecorder(IUpdateSource inner) : IUpdateSource
    {
        public IReadOnlyList<VelopackAsset> Releases { get; private set; } = [];

        public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
        {
            var feed = await inner.GetReleaseFeed(logger, appId, channel, stagingId, latestLocalRelease).ConfigureAwait(false);
            Releases = [.. feed.Assets.Where(asset => asset.Type == VelopackAssetType.Full)];
            return feed;
        }

        public Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releasesToDownload, string localFile, Action<int> progress, CancellationToken cancelToken) =>
            inner.DownloadReleaseEntry(logger, releasesToDownload, localFile, progress, cancelToken);
    }
}
