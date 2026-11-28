using System.Text;
using System.Text.Json;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Velopack.Logging;
using Velopack.Sources;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public sealed class DesktopGithubSourceTests(ITestOutputHelper output)
{
    private const string Repository = "https://github.com/example/tasks";

    [Fact]
    public async Task bounds_feed_downloads_to_the_ten_highest_desktop_versions()
    {
        var releases = Enumerable.Range(0, 1500).Select(i => Release($"android-v1.0.{i}", "1.0.0")).ToList();
        releases.AddRange(Enumerable.Range(1, 200).Select(i =>
            Release($"v{i}.0.0", $"{i}.0.0", publishedAt: i <= 10 ? "2026-02-01T00:00:00Z" : "2026-01-01T00:00:00Z")));
        var download = new Downloads(releases);
        var source = new DesktopGithubSource(Repository, new InMemoryLogSink().CreateLogger(), download);
        var allocated = GC.GetTotalAllocatedBytes();
        var timer = System.Diagnostics.Stopwatch.StartNew();

        var feed = await source.GetReleaseFeed(new QuietLog(), "MemoriaToDo", "win");

        output.WriteLine($"1700 release records: {timer.Elapsed.TotalMilliseconds:F1} ms; {GC.GetTotalAllocatedBytes() - allocated} allocated bytes.");
        Assert.Equal(10, download.FeedDownloads);
        Assert.Equal(Enumerable.Range(191, 10).Select(i => $"{i}.0.0").Order(),
            feed.Assets.Select(asset => asset.Version.ToString()).Order());
    }

    [Fact]
    public async Task finds_desktop_updates_after_more_than_one_page_of_Android_releases()
    {
        var releases = Enumerable.Range(0, 101).Select(i => Release($"android-v1.0.{i}", "1.0.0")).ToList();
        releases.Add(Release("v2.0.0", "2.0.0"));
        releases.Add(Release("v1.0.0", "1.0.0"));
        var download = new Downloads(releases);
        var log = new InMemoryLogSink();
        var source = new DesktopGithubSource(Repository, log.CreateLogger(), download);

        var feed = await source.GetReleaseFeed(new QuietLog(), "MemoriaToDo", "win");

        Assert.Equal(["1.0.0", "2.0.0"], feed.Assets.Select(a => a.Version.ToString()).Order());
        Assert.NotEmpty(log.Events.At(LogEventLevel.Information, Repository));
    }

    [Fact]
    public async Task ignores_other_tag_families_drafts_and_prereleases()
    {
        var download = new Downloads([
            Release("android-v9.0.0", "9.0.0"), Release("proof-1", "8.0.0"),
            Release("v7.0.0", "7.0.0", draft: true), Release("v6.0.0", "6.0.0", prerelease: true),
            Release("v2.0.0", "2.0.0"),
        ]);
        var source = new DesktopGithubSource(Repository, new InMemoryLogSink().CreateLogger(), download);

        var feed = await source.GetReleaseFeed(new QuietLog(), "MemoriaToDo", "win");

        Assert.Equal("2.0.0", Assert.Single(feed.Assets).Version.ToString());
    }

    [Fact]
    public async Task reports_a_failed_request_instead_of_reporting_no_update()
    {
        var failure = new HttpRequestException("GitHub is unavailable");
        var download = new Downloads([]) { Failure = failure };
        var log = new InMemoryLogSink();
        var source = new DesktopGithubSource(Repository, log.CreateLogger(), download);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(
            () => source.GetReleaseFeed(new QuietLog(), "MemoriaToDo", "win"));

        Assert.Same(failure, thrown);
        Assert.Empty(log.Events.At(LogEventLevel.Error, Repository));
    }

    [Fact]
    public async Task refuses_a_null_release_response_instead_of_an_empty_success()
    {
        var source = new DesktopGithubSource(Repository, new InMemoryLogSink().CreateLogger(),
            new Downloads([]) { Response = "null" });

        await Assert.ThrowsAsync<JsonException>(() => source.GetReleaseFeed(new QuietLog(), "MemoriaToDo", "win"));
    }

    [Fact]
    public async Task ignores_a_release_whose_tag_has_extra_text_after_the_version()
    {
        var source = new DesktopGithubSource(Repository, new InMemoryLogSink().CreateLogger(),
            new Downloads([Release("v4.0.0\n", "4.0.0"), Release("v2.0.0", "2.0.0")]));

        var feed = await source.GetReleaseFeed(new QuietLog(), "MemoriaToDo", "win");

        Assert.Equal("2.0.0", Assert.Single(feed.Assets).Version.ToString());
    }

    [Fact]
    public async Task refuses_a_release_without_its_prerelease_status()
    {
        var release = JsonSerializer.SerializeToElement(Release("v2.0.0", "2.0.0"));
        var fields = release.EnumerateObject().Where(property => property.Name != "prerelease")
            .ToDictionary(property => property.Name, property => property.Value);
        var log = new InMemoryLogSink();
        var source = new DesktopGithubSource(Repository, log.CreateLogger(),
            new Downloads([]) { Response = JsonSerializer.Serialize(new[] { fields }) });

        await Assert.ThrowsAsync<JsonException>(
            () => source.GetReleaseFeed(new QuietLog(), "MemoriaToDo", "win"));

        Assert.Empty(log.Events.At(LogEventLevel.Error, Repository));
    }

    private static object Release(string tag, string version, bool draft = false, bool prerelease = false,
        string publishedAt = "2026-01-01T00:00:00Z") => new
    {
        tag_name = tag, name = tag, draft, prerelease, published_at = publishedAt,
        assets = new[] { new { name = "releases.win.json", browser_download_url = $"https://example.invalid/{version}/releases.win.json" } },
    };

    private sealed class Downloads(IReadOnlyList<object> releases) : IFileDownloader
    {
        public int FeedDownloads { get; private set; }
        public Exception? Failure { get; init; }
        public string? Response { get; init; }

        public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            if (Failure is not null) return Task.FromException<string>(Failure);
            if (Response is not null) return Task.FromResult(Response);
            var query = new Uri(url).Query.TrimStart('?').Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
            var size = int.Parse(query["per_page"]);
            var page = int.Parse(query["page"]);
            return Task.FromResult(JsonSerializer.Serialize(releases.Skip((page - 1) * size).Take(size)));
        }

        public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            FeedDownloads++;
            var version = new Uri(url).Segments[1].TrimEnd('/');
            return Task.FromResult(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                Assets = new[] { new { PackageId = "MemoriaToDo", Version = version, Type = "Full",
                    FileName = $"MemoriaToDo-{version}-full.nupkg", SHA1 = new string('a', 40), SHA256 = new string('b', 64), Size = 1 } },
            })));
        }

        public Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null,
            double timeout = 30, CancellationToken cancelToken = default) =>
            throw new InvalidOperationException("A check must not download a package.");
    }

    private sealed class QuietLog : IVelopackLogger
    {
        public void Log(VelopackLogLevel logLevel, string? message, Exception? exception) { }
    }
}
