using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>A selection changes all or nothing: when a part can't read the folder, no part changes.</summary>
[Unit]
public sealed class SyncedRepositorySelectFailureTests
{
    private const string Old = "old-clone";
    private const string Clone = RepositoryRig.Clone;
    private readonly RepositoryRig rig = new();

    [Fact]
    public void Lets_every_other_part_read_when_a_part_fails_to_read()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        var c = rig.Add("c");
        b.LoadFailure = new IOException("b unreadable");

        Assert.Throws<IOException>(() => rig.Repository.Select(Clone));

        Assert.Equal([Clone], a.ReadFolders);
        Assert.Equal([Clone], b.ReadFolders);
        Assert.Equal([Clone], c.ReadFolders);
    }

    [Fact]
    public void Changes_nothing_when_a_part_fails_to_read()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        rig.Repository.Select(Old);
        rig.ForgetSteps();
        b.LoadFailure = new IOException("b unreadable");

        Assert.Throws<IOException>(() => rig.Repository.Select(Clone));

        Assert.Equal(Old, rig.Repository.Folder);
        Assert.Equal(1, a.Applies);
        Assert.Equal(1, b.Applies);
        Assert.Equal(1, a.Notices);
        Assert.Equal(1, b.Notices);
        Assert.DoesNotContain(rig.Steps, step => step.EndsWith(".Apply", StringComparison.Ordinal) || step.EndsWith(".Loaded", StringComparison.Ordinal));
    }

    [Fact]
    public void Leaves_no_folder_selected_when_the_first_selection_fails()
    {
        var part = rig.Add("a");
        part.LoadFailure = new IOException("unreadable");

        Assert.Throws<IOException>(() => rig.Repository.Select(Clone));

        Assert.Null(rig.Repository.Folder);
        Assert.Equal(0, part.Applies);
        Assert.Equal(0, part.Notices);
    }

    [Fact]
    public void Throws_the_one_failure_as_the_same_exception_object()
    {
        rig.Add("a");
        var b = rig.Add("b");
        var failure = new IOException("b unreadable");
        b.LoadFailure = failure;

        var thrown = Assert.Throws<IOException>(() => rig.Repository.Select(Clone));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Throws_several_failures_as_an_aggregate_in_the_order_the_parts_were_added()
    {
        var a = rig.Add("a");
        rig.Add("b");
        var c = rig.Add("c");
        var first = new IOException("a unreadable");
        var second = new InvalidDataException("c unreadable");
        a.LoadFailure = first;
        c.LoadFailure = second;

        var thrown = Assert.Throws<AggregateException>(() => rig.Repository.Select(Clone));

        Assert.Equal([first, second], thrown.InnerExceptions);
    }

    [Fact]
    public void Raises_every_parts_notice_when_an_earlier_parts_notice_fails()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        var c = rig.Add("c");
        a.WhenLoaded = () => throw new IOException("a notice failed");

        Assert.Throws<IOException>(() => rig.Repository.Select(Clone));

        Assert.Equal(1, a.Notices);
        Assert.Equal(1, b.Notices);
        Assert.Equal(1, c.Notices);
        Assert.Equal(Clone, rig.Repository.Folder);
    }

    [Fact]
    public void Throws_the_one_notice_failure_as_the_same_exception_object()
    {
        rig.Add("a");
        var b = rig.Add("b");
        var failure = new IOException("b notice failed");
        b.WhenLoaded = () => throw failure;

        var thrown = Assert.Throws<IOException>(() => rig.Repository.Select(Clone));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Throws_several_notice_failures_as_an_aggregate_in_the_order_the_parts_were_added()
    {
        var a = rig.Add("a");
        rig.Add("b");
        var c = rig.Add("c");
        var first = new IOException("a notice failed");
        var second = new InvalidDataException("c notice failed");
        a.WhenLoaded = () => throw first;
        c.WhenLoaded = () => throw second;

        var thrown = Assert.Throws<AggregateException>(() => rig.Repository.Select(Clone));

        Assert.Equal([first, second], thrown.InnerExceptions);
    }
}
