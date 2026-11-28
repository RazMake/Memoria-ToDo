using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>Which page the description browser loads and when, through <c>PageLoads</c>: one page loads at a time and the latest asked for wins.</summary>
[Unit]
public class PageLoadsTests
{
    [Fact]
    public void Holds_only_the_latest_page_asked_for_until_the_browser_is_ready()
    {
        var loads = new PageLoads();

        Assert.Null(loads.Request("first"));
        Assert.Null(loads.Request("second"));
        Assert.False(loads.IsLoading);

        Assert.Equal("second", loads.Ready());
        Assert.True(loads.IsLoading);
    }

    [Fact]
    public void Starts_nothing_when_the_browser_becomes_ready_with_no_page_asked_for()
    {
        var loads = new PageLoads();

        Assert.Null(loads.Ready());
        Assert.False(loads.IsLoading);
    }

    [Fact]
    public void Loads_a_page_asked_for_at_once_when_the_browser_is_ready_and_nothing_is_loading()
    {
        var loads = new PageLoads();
        loads.Ready();

        Assert.Equal("first", loads.Request("first"));
        Assert.True(loads.IsLoading);
    }

    [Fact]
    public void Waits_for_the_loading_page_to_end_then_loads_only_the_latest_page_asked_for_meanwhile()
    {
        var loads = new PageLoads();
        loads.Ready();
        Assert.Equal("first", loads.Request("first"));

        Assert.Null(loads.Request("second"));
        Assert.Null(loads.Request("third"));
        Assert.True(loads.IsLoading);

        Assert.Equal("third", loads.Complete());
        Assert.True(loads.IsLoading);
        Assert.Null(loads.Complete());
        Assert.False(loads.IsLoading);
    }

    [Fact]
    public void Is_not_loading_after_the_loading_page_ends_with_no_page_asked_for_meanwhile()
    {
        var loads = new PageLoads();
        loads.Ready();
        loads.Request("first");

        Assert.Null(loads.Complete());
        Assert.False(loads.IsLoading);
        Assert.Equal("second", loads.Request("second"));
    }

    [Fact]
    public void Queues_a_page_asked_for_while_the_first_one_asked_before_ready_is_loading()
    {
        var loads = new PageLoads();
        loads.Request("first");
        Assert.Equal("first", loads.Ready());

        Assert.Null(loads.Request("second"));
        Assert.Equal("second", loads.Complete());
    }

    [Fact]
    public void Forgets_the_page_waiting_when_cleared()
    {
        var loads = new PageLoads();
        loads.Request("waiting");
        loads.Clear();

        Assert.Null(loads.Ready());
        Assert.False(loads.IsLoading);
    }

    [Fact]
    public void Forgets_the_page_queued_behind_a_loading_page_when_cleared()
    {
        var loads = new PageLoads();
        loads.Ready();
        loads.Request("loading");
        loads.Request("queued");
        loads.Clear();

        Assert.Null(loads.Complete());
    }

    [Fact]
    public void Accepts_an_empty_page_as_a_page()
    {
        var loads = new PageLoads();
        loads.Ready();

        Assert.Equal("", loads.Request(""));
    }

    [Fact]
    public void Rejects_a_missing_page_and_keeps_the_page_waiting()
    {
        var loads = new PageLoads();
        loads.Request("waiting");

        Assert.Throws<ArgumentNullException>(() => loads.Request(null!));
        Assert.Equal("waiting", loads.Ready());
    }
}
