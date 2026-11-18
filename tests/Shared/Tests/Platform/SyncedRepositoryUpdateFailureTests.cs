using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>When the update or a part's reload fails, every other part still reloads, and the failures are kept.</summary>
[Unit]
public sealed class SyncedRepositoryUpdateFailureTests
{
    private const string Clone = RepositoryRig.Clone;
    private readonly RepositoryRig rig = new();

    [Fact]
    public void Reloads_and_notifies_every_part_when_the_update_fails()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        rig.Repository.Select(Clone);
        rig.ForgetSteps();

        Assert.Throws<IOException>(() => rig.Repository.Update(Clone, () => throw new IOException("merge failed")));

        Assert.Equal(["a.Load(clone)", "a.Apply", "b.Load(clone)", "b.Apply", "a.Loaded", "b.Loaded"], rig.Steps);
        Assert.Equal(2, a.Notices);
        Assert.Equal(2, b.Notices);
    }

    [Fact]
    public void Throws_the_updates_failure_as_the_same_exception_object()
    {
        rig.Add("a");
        rig.Repository.Select(Clone);
        var failure = new IOException("merge failed");

        var thrown = Assert.Throws<IOException>(() => rig.Repository.Update(Clone, () => throw failure));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Lets_the_other_parts_reload_and_notify_when_one_part_fails_to_reload()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        var c = rig.Add("c");
        rig.Repository.Select(Clone);
        b.LoadFailure = new IOException("b unreadable");

        Assert.Throws<IOException>(() => rig.Repository.Update(Clone, () => { }));

        Assert.Equal(2, a.Applies);
        Assert.Equal(2, c.Applies);
        Assert.Equal(2, a.Notices);
        Assert.Equal(2, c.Notices);
    }

    [Fact]
    public void Keeps_the_state_of_a_part_that_fails_to_reload_and_raises_no_notice_for_it()
    {
        var b = rig.Add("b");
        rig.Repository.Select(Clone);
        b.LoadFailure = new IOException("b unreadable");

        Assert.Throws<IOException>(() => rig.Repository.Update(Clone, () => { }));

        Assert.Equal(1, b.Applies);
        Assert.Equal(1, b.Notices);
    }

    [Fact]
    public void Throws_a_single_reload_failure_as_the_same_exception_object()
    {
        rig.Add("a");
        var b = rig.Add("b");
        rig.Repository.Select(Clone);
        var failure = new IOException("b unreadable");
        b.LoadFailure = failure;

        var thrown = Assert.Throws<IOException>(() => rig.Repository.Update(Clone, () => { }));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Throws_several_reload_failures_as_an_aggregate_in_the_order_the_parts_were_added()
    {
        var a = rig.Add("a");
        rig.Add("b");
        var c = rig.Add("c");
        rig.Repository.Select(Clone);
        var first = new IOException("a unreadable");
        var second = new InvalidDataException("c unreadable");
        a.LoadFailure = first;
        c.LoadFailure = second;

        var thrown = Assert.Throws<AggregateException>(() => rig.Repository.Update(Clone, () => { }));

        Assert.Equal([first, second], thrown.InnerExceptions);
    }

    [Fact]
    public void Throws_the_updates_failure_first_then_each_failed_reload_in_the_order_the_parts_were_added()
    {
        var a = rig.Add("a");
        rig.Add("b");
        var c = rig.Add("c");
        rig.Repository.Select(Clone);
        var update = new InvalidOperationException("merge failed");
        var firstReload = new IOException("a unreadable");
        var secondReload = new InvalidDataException("c unreadable");
        a.LoadFailure = firstReload;
        c.LoadFailure = secondReload;

        var thrown = Assert.Throws<AggregateException>(() => rig.Repository.Update(Clone, () => throw update));

        Assert.Equal([update, firstReload, secondReload], thrown.InnerExceptions);
    }

    [Fact]
    public void Keeps_both_failures_when_the_update_and_one_reload_fail()
    {
        var b = rig.Add("b");
        rig.Repository.Select(Clone);
        var update = new InvalidOperationException("merge failed");
        var reload = new IOException("b unreadable");
        b.LoadFailure = reload;

        var thrown = Assert.Throws<AggregateException>(() => rig.Repository.Update(Clone, () => throw update));

        Assert.Equal([update, reload], thrown.InnerExceptions);
    }

    [Fact]
    public void Raises_every_parts_notice_when_an_earlier_parts_notice_fails()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        var c = rig.Add("c");
        rig.Repository.Select(Clone);
        a.WhenLoaded = () => throw new IOException("a notice failed");

        Assert.Throws<IOException>(() => rig.Repository.Update(Clone, () => { }));

        Assert.Equal(2, a.Notices);
        Assert.Equal(2, b.Notices);
        Assert.Equal(2, c.Notices);
    }

    [Fact]
    public void Throws_the_one_notice_failure_as_the_same_exception_object()
    {
        rig.Add("a");
        var b = rig.Add("b");
        rig.Repository.Select(Clone);
        var failure = new IOException("b notice failed");
        b.WhenLoaded = () => throw failure;

        var thrown = Assert.Throws<IOException>(() => rig.Repository.Update(Clone, () => { }));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Throws_several_notice_failures_as_an_aggregate_in_the_order_the_parts_were_added()
    {
        var a = rig.Add("a");
        rig.Add("b");
        var c = rig.Add("c");
        rig.Repository.Select(Clone);
        var first = new IOException("a notice failed");
        var second = new InvalidDataException("c notice failed");
        a.WhenLoaded = () => throw first;
        c.WhenLoaded = () => throw second;

        var thrown = Assert.Throws<AggregateException>(() => rig.Repository.Update(Clone, () => { }));

        Assert.Equal([first, second], thrown.InnerExceptions);
    }

    [Fact]
    public void Throws_the_updates_failure_then_the_reload_failures_then_the_notice_failures_each_in_the_order_the_parts_were_added()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        var c = rig.Add("c");
        var d = rig.Add("d");
        rig.Repository.Select(Clone);
        var update = new InvalidOperationException("merge failed");
        var reload = new IOException("b unreadable");
        var firstNotice = new InvalidDataException("a notice failed");
        var secondNotice = new FormatException("d notice failed");
        b.LoadFailure = reload;
        a.WhenLoaded = () => throw firstNotice;
        d.WhenLoaded = () => throw secondNotice;

        var thrown = Assert.Throws<AggregateException>(() => rig.Repository.Update(Clone, () => throw update));

        Assert.Equal([update, reload, firstNotice, secondNotice], thrown.InnerExceptions);
        Assert.Equal(2, c.Notices);
    }
}
