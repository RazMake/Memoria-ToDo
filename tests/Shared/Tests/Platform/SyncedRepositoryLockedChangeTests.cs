using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>
/// A locked change is a locked call for a change that raises notices: it can't start when the thread already holds
/// the lock, because the notices would then run under it.
/// </summary>
[Unit]
public sealed class SyncedRepositoryLockedChangeTests
{
    private const string Clone = RepositoryRig.Clone;
    private readonly RepositoryRig rig = new();

    [Fact]
    public void Gives_a_locked_change_no_folder_before_any_selection_and_returns_its_value()
    {
        var result = rig.Repository.LockedChange(folder => folder is null ? "none" : folder);

        Assert.Equal("none", result);
    }

    [Fact]
    public void Gives_a_locked_change_the_selected_folder_and_returns_its_value()
    {
        rig.Repository.Select(Clone);

        var result = rig.Repository.LockedChange(folder => "saw " + folder);

        Assert.Equal("saw " + Clone, result);
    }

    [Fact]
    public void Gives_a_locked_change_action_the_current_folder_each_time_it_runs()
    {
        var seen = new List<string?>();
        rig.Repository.LockedChange(folder => seen.Add(folder));
        rig.Repository.Select(Clone);
        rig.Repository.LockedChange(folder => seen.Add(folder));

        Assert.Equal(new string?[] { null, Clone }, seen);
    }

    [Fact]
    public void Lets_a_failure_of_a_locked_change_out_and_releases_the_lock()
    {
        var failure = new IOException("failed");

        var thrown = Assert.Throws<IOException>(() => rig.Repository.LockedChange<int>(_ => throw failure));

        Assert.Same(failure, thrown);
        Assert.True(rig.AnotherThreadCanLock());
    }

    [Fact]
    public void Refuses_a_locked_change_inside_a_locked_call_without_running_it()
    {
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Locked(_ => rig.Repository.LockedChange(_ =>
        {
            ran = true;
            return 1;
        })));

        Assert.False(ran);
    }

    [Fact]
    public void Refuses_a_locked_change_action_inside_a_locked_action_without_running_it()
    {
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Locked(_ => rig.Repository.LockedChange(_ => { ran = true; })));

        Assert.False(ran);
    }

    [Fact]
    public void Refuses_a_locked_change_inside_another_locked_change_without_running_it()
    {
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.LockedChange(_ => rig.Repository.LockedChange(_ =>
        {
            ran = true;
            return 1;
        })));

        Assert.False(ran);
    }

    [Fact]
    public void Refuses_a_locked_change_action_inside_another_locked_change_action_without_running_it()
    {
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.LockedChange(_ => rig.Repository.LockedChange(_ => { ran = true; })));

        Assert.False(ran);
    }

    [Fact]
    public void Keeps_the_lock_usable_after_refusing_a_locked_change_inside_it()
    {
        Assert.Throws<InvalidOperationException>(() => rig.Repository.Locked(_ => rig.Repository.LockedChange(_ => { })));

        var result = rig.Repository.LockedChange(_ => 7);

        Assert.Equal(7, result);
        Assert.True(rig.AnotherThreadCanLock());
    }

    [Fact]
    public void Still_lets_a_locked_call_take_the_lock_inside_a_locked_change()
    {
        var result = rig.Repository.LockedChange(_ => rig.Repository.Locked(_ => 5));

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task Makes_another_threads_locked_action_wait_until_a_locked_change_returns()
    {
        Task? other = null;

        rig.Repository.LockedChange(_ =>
        {
            other = rig.LockOnAnotherThread("other");
            rig.Note("change done");
        });

        await other!.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        Assert.Equal(["change done", "other"], rig.Steps);
    }
}
