using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>Whether the one page of releases Velopack reads from GitHub may leave out notes of releases after the running version.</summary>
[Unit]
public class GithubFeedTests
{
    [Fact]
    public void A_full_page_whose_oldest_release_is_newer_than_the_running_version_may_leave_releases_out()
    {
        Assert.True(VelopackUpdates.FeedStopsAfter(Enumerable.Range(11, 10), current: 5));
    }

    [Fact]
    public void A_full_page_that_reaches_the_running_version_leaves_nothing_out()
    {
        Assert.False(VelopackUpdates.FeedStopsAfter(Enumerable.Range(5, 10), current: 5));
    }

    [Fact]
    public void A_short_feed_whose_oldest_release_is_newer_than_the_running_version_may_leave_releases_out()
    {
        Assert.True(VelopackUpdates.FeedStopsAfter(Enumerable.Range(11, 3), current: 5));
    }

    [Fact]
    public void A_feed_holding_the_running_version_exactly_leaves_nothing_out()
    {
        Assert.False(VelopackUpdates.FeedStopsAfter([9, 5, 7], current: 5));
    }

    [Fact]
    public void An_empty_feed_may_leave_releases_out()
    {
        Assert.True(VelopackUpdates.FeedStopsAfter(Array.Empty<int>(), current: 5));
    }

    [Fact]
    public void An_unordered_page_is_judged_by_its_oldest_release()
    {
        int[] feed = [20, 12, 19, 4, 18, 17, 16, 15, 14, 13];

        Assert.False(VelopackUpdates.FeedStopsAfter(feed, current: 5));
    }
}
