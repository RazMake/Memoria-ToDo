using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Selecting a folder in the synced repository: every part reads, then all make it current, then they notify.</summary>
[Unit]
public sealed class SyncedRepositorySelectTests
{
    private const string Clone = RepositoryRig.Clone;
    private readonly RepositoryRig rig = new();

    [Fact]
    public void Reads_every_part_in_order_then_makes_each_current_then_notifies_each_in_the_same_order()
    {
        rig.Add("a");
        rig.Add("b");

        rig.Repository.Select(Clone);

        Assert.Equal(
            ["a.Load(clone)", "b.Load(clone)", "a.Apply", "b.Apply", "a.Loaded", "b.Loaded"],
            rig.Steps);
    }

    [Fact]
    public void Has_the_folder_selected_when_the_parts_are_notified()
    {
        var part = rig.Add("a");
        string? seen = "not notified";
        part.WhenLoaded = () => seen = rig.Repository.Folder;

        rig.Repository.Select(Clone);

        Assert.Equal(Clone, seen);
        Assert.Equal(Clone, rig.Repository.Folder);
    }

    [Fact]
    public void Notifies_on_the_calling_thread()
    {
        var part = rig.Add("a");
        var notifiedOn = -1;
        part.WhenLoaded = () => notifiedOn = Environment.CurrentManagedThreadId;

        rig.Repository.Select(Clone);

        Assert.Equal(Environment.CurrentManagedThreadId, notifiedOn);
    }

    [Fact]
    public void Notifies_after_releasing_the_lock_so_a_handler_can_use_it_from_another_thread()
    {
        var part = rig.Add("a");
        var otherThreadLocked = false;
        part.WhenLoaded = () => otherThreadLocked = rig.AnotherThreadCanLock();

        rig.Repository.Select(Clone);

        Assert.True(otherThreadLocked);
    }

    [Fact]
    public async Task Holds_the_lock_while_the_parts_read_and_make_it_current()
    {
        var part = rig.Add("a");
        Task? duringRead = null;
        Task? duringApply = null;
        part.WhenReading = _ => duringRead = rig.LockOnAnotherThread("other during read");
        part.WhenApplying = () => duringApply = rig.LockOnAnotherThread("other during apply");

        rig.Repository.Select(Clone);

        await Task.WhenAll(duringRead!, duringApply!).WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        var steps = rig.Steps;
        Assert.True(steps.IndexOf("other during read") > steps.IndexOf("a.Apply"));
        Assert.True(steps.IndexOf("other during apply") > steps.IndexOf("a.Apply"));
    }

    [Fact]
    public void Selects_the_folder_with_no_part()
    {
        rig.Repository.Select(Clone);

        Assert.Equal(Clone, rig.Repository.Folder);
    }

    [Fact]
    public void Empties_every_part_and_notifies_when_the_folder_is_cleared()
    {
        var a = rig.Add("a");
        var b = rig.Add("b");
        rig.Repository.Select(Clone);
        rig.ForgetSteps();

        rig.Repository.Select(null);

        Assert.Equal(["a.Load(null)", "b.Load(null)", "a.Apply", "b.Apply", "a.Loaded", "b.Loaded"], rig.Steps);
        Assert.Null(rig.Repository.Folder);
        Assert.Equal(2, a.Notices);
        Assert.Equal(2, b.Notices);
    }

    [Fact]
    public void Notifies_when_no_folder_was_ever_selected_and_none_is_selected()
    {
        var part = rig.Add("a");

        rig.Repository.Select(null);

        Assert.Equal(new string?[] { null }, part.ReadFolders);
        Assert.Null(rig.Repository.Folder);
        Assert.Equal(1, part.Notices);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Rejects_a_blank_folder_before_any_part_loads_and_keeps_the_selected_folder(string blank)
    {
        var part = rig.Add("a");
        rig.Repository.Select(Clone);
        rig.ForgetSteps();

        Assert.Throws<ArgumentException>(() => rig.Repository.Select(blank));

        Assert.Empty(rig.Steps);
        Assert.Equal(Clone, rig.Repository.Folder);
        Assert.Equal(1, part.Notices);
    }
}
