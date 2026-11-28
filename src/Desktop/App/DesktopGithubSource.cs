using Serilog;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Velopack.Sources;

namespace Memoria.ToDo.Desktop.App;

internal sealed class DesktopGithubSource : GithubSource
{
    private static readonly (Regex Tag, int MaximumFeeds) DesktopRules = ReadReleaseRules();
    private static readonly JsonSerializerOptions ReleaseJson = new() { RespectRequiredConstructorParameters = true };
    private readonly ILogger _logger;

    public DesktopGithubSource(string repositoryUrl, ILogger logger, IFileDownloader? downloader = null)
        : base(repositoryUrl, accessToken: null, prerelease: false, downloader)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        _logger.Information("Reading desktop releases from {Repository}", RepoUri.ToString());
        const int pageSize = 100;
        var found = new PriorityQueue<GithubRelease, (BigInteger Major, BigInteger Minor, BigInteger Patch)>();
        var discarded = 0;
        for (var page = 1; ; page++)
        {
            var uri = new Uri(GetApiBaseUrl(RepoUri), $"repos{RepoUri.AbsolutePath}/releases?per_page={pageSize}&page={page}");
            var response = await Downloader.DownloadString(uri.ToString(), GetRequestHeaders("application/vnd.github.v3+json")).ConfigureAwait(false);
            var releases = JsonSerializer.Deserialize<TaggedRelease[]>(response, ReleaseJson)
                ?? throw new JsonException("GitHub returned a null release list.");
            foreach (var release in releases)
            {
                if (release is null || release.Tag is null || release.Assets is null)
                    throw new JsonException("GitHub returned an incomplete release.");
                var tag = DesktopRules.Tag.Match(release.Tag);
                if (tag.Success && !release.Draft && !release.Prerelease)
                {
                    var version = (BigInteger.Parse(tag.Groups[1].Value), BigInteger.Parse(tag.Groups[2].Value), BigInteger.Parse(tag.Groups[3].Value));
                    if (found.Count < DesktopRules.MaximumFeeds) found.Enqueue(release, version);
                    else
                    {
                        found.EnqueueDequeue(release, version);
                        discarded++;
                    }
                }
                else discarded++;
            }
            if (releases.Length < pageSize) break;
        }
        _logger.Debug("Selected up to {Limit} highest desktop versions with tag rule {TagRule}, excluding drafts and prereleases; discarded {Discarded} from {Repository}",
            DesktopRules.MaximumFeeds, DesktopRules.Tag.ToString(), discarded, RepoUri.ToString());
        _logger.Information("Read {Count} desktop releases from {Repository}", found.Count, RepoUri.ToString());
        return [.. found.UnorderedItems.Select(item => item.Element).OrderByDescending(release => release.PublishedAt)];
    }

    private static (Regex Tag, int MaximumFeeds) ReadReleaseRules()
    {
        using var stream = typeof(DesktopGithubSource).Assembly.GetManifestResourceStream("release-platforms.json")
            ?? throw new InvalidOperationException("The release platform definitions are missing.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var desktop = root.GetProperty("platforms").GetProperty("desktop");
        var prefix = desktop.GetProperty("tagPrefix").GetString()
            ?? throw new JsonException("The desktop tag prefix is missing.");
        var version = root.GetProperty("versionPattern").GetString()
            ?? throw new JsonException("The release version pattern is missing.");
        var maximumFeeds = desktop.GetProperty("maximumReleaseFeeds").GetInt32();
        if (maximumFeeds <= 0) throw new JsonException("The desktop release-feed limit must be positive.");
        return (new Regex($"\\A{Regex.Escape(prefix)}{version}\\z", RegexOptions.CultureInvariant), maximumFeeds);
    }

    private sealed class TaggedRelease : GithubRelease
    {
        [JsonConstructor]
        public TaggedRelease(string tag, bool draft, bool prerelease)
        {
            Tag = tag ?? throw new JsonException("GitHub returned a null release tag.");
            Draft = draft;
            Prerelease = prerelease;
        }

        [JsonRequired, JsonPropertyName("tag_name")]
        public string? Tag { get; set; }

        [JsonRequired, JsonPropertyName("draft")]
        public bool Draft { get; set; }
    }
}
