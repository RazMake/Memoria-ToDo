using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Sync's update of the clone: checks the folder, runs under the lock, then reloads every part.</summary>
[Unit]
public sealed class SyncedRepositoryUpdateTests
{
    private const string Clone = RepositoryRig.Clone;
    private readonly RepositoryRig rig = new();

    [Fact]
    public void Refuses_a_missing_folder_or_update()
    {
        rig.Repository.Select(Clone);

        Assert.Throws<ArgumentNullException>(() => rig.Repository.Update(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => rig.Repository.Update(Clone, null!));
    }

    [Fact]
    public void Refuses_an_update_when_no_folder_is_selected_without_running_it_or_loading_a_part()
    {
        var part = rig.Add("a");
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Update(Clone, () => ran = true));

        Assert.False(ran);
        Assert.Empty(rig.Steps);
        Assert.Equal(0, part.Notices);
    }

    [Theory]
    [InlineData("other-clone")]
    [InlineData("CLONE")]
    [InlineData("clone/")]
    public void Refuses_an_update_for_a_folder_that_is_not_the_selected_one_without_running_it_or_loading_a_part(string expected)
    {
        var part = rig.Add("a");
        rig.Repository.Select(Clone);
        rig.ForgetSteps();
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Update(expected, () => ran = true));

        Assert.False(ran);
        Assert.Empty(rig.Steps);
        Assert.Equal(1, part.Notices);
    }

    [Fact]
    public async Task Runs_the_update_under_the_lock()
    {
        rig.Add("a");
        rig.Repository.Select(Clone);
        Task? other = null;

        rig.Repository.Update(Clone, () =>
        {
            other = rig.LockOnAnotherThread("other");
            rig.Note("update done");
        });

        await other!.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        var steps = rig.Steps;
        Assert.True(steps.IndexOf("other") > steps.IndexOf("update done"));
    }

    [Fact]
    public void Reloads_each_part_with_the_expected_folder_reading_then_making_current_one_part_at_a_time_then_notifies_each()
    {
        rig.Add("a");
        rig.Add("b");
        rig.Repository.Select(Clone);
        rig.ForgetSteps();

        rig.Repository.Update(Clone, () => rig.Note("update"));

        Assert.Equal(
            ["update", "a.Load(clone)", "a.Apply", "b.Load(clone)", "b.Apply", "a.Loaded", "b.Loaded"],
            rig.Steps);
    }

    [Fact]
    public async Task Keeps_the_lock_while_every_part_reloads()
    {
        var part = rig.Add("a");
        rig.Add("b");
        rig.Repository.Select(Clone);
        rig.ForgetSteps();
        Task? other = null;
        part.WhenApplying = () => other = rig.LockOnAnotherThread("other");

        rig.Repository.Update(Clone, () => { });

        await other!.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        var steps = rig.Steps;
        Assert.True(steps.IndexOf("other") > steps.IndexOf("b.Apply"));
    }

    [Fact]
    public void Keeps_the_selected_folder_after_an_update()
    {
        rig.Add("a");
        rig.Repository.Select(Clone);

        rig.Repository.Update(Clone, () => { });

        Assert.Equal(Clone, rig.Repository.Folder);
    }

    [Fact]
    public void Notifies_on_the_calling_thread_after_releasing_the_lock()
    {
        var part = rig.Add("a");
        rig.Repository.Select(Clone);
        var otherThreadLocked = false;
        var notifiedOn = -1;
        part.WhenLoaded = () =>
        {
            notifiedOn = Environment.CurrentManagedThreadId;
            otherThreadLocked = rig.AnotherThreadCanLock();
        };

        rig.Repository.Update(Clone, () => { });

        Assert.True(otherThreadLocked);
        Assert.Equal(Environment.CurrentManagedThreadId, notifiedOn);
    }
}
