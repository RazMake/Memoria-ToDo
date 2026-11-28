using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The synced repository's lock: what a locked call sees, who waits, and what can't run inside it.</summary>
[Unit]
public sealed class SyncedRepositoryLockTests
{
    private const string Clone = RepositoryRig.Clone;
    private readonly RepositoryRig rig = new();

    [Fact]
    public void Gives_a_locked_call_no_folder_before_any_selection_and_returns_its_value()
    {
        var result = rig.Repository.Locked(folder => folder is null ? "none" : folder);

        Assert.Equal("none", result);
    }

    [Fact]
    public void Gives_a_locked_call_the_selected_folder_and_returns_its_value()
    {
        rig.Repository.Select(Clone);

        var result = rig.Repository.Locked(folder => "saw " + folder);

        Assert.Equal("saw " + Clone, result);
    }

    [Fact]
    public void Gives_a_locked_action_the_current_folder_each_time_it_runs()
    {
        var seen = new List<string?>();
        rig.Repository.Locked(folder => seen.Add(folder));
        rig.Repository.Select(Clone);
        rig.Repository.Locked(folder => seen.Add(folder));

        Assert.Equal(new string?[] { null, Clone }, seen);
    }

    [Fact]
    public void Lets_a_failure_of_a_locked_call_out_and_releases_the_lock()
    {
        var failure = new IOException("failed");

        var thrown = Assert.Throws<IOException>(() => rig.Repository.Locked<int>(_ => throw failure));

        Assert.Same(failure, thrown);
        Assert.True(rig.AnotherThreadCanLock());
    }

    [Fact]
    public void Lets_a_failure_of_a_locked_action_out_and_releases_the_lock()
    {
        var failure = new IOException("failed");

        var thrown = Assert.Throws<IOException>(() => rig.Repository.Locked(_ => throw failure));

        Assert.Same(failure, thrown);
        Assert.True(rig.AnotherThreadCanLock());
    }

    [Fact]
    public void Lets_a_locked_call_take_the_lock_again_on_the_same_thread()
    {
        var result = rig.Repository.Locked(_ => rig.Repository.Locked(_ => 5));

        Assert.Equal(5, result);
    }

    [Fact]
    public void Lets_a_locked_action_take_the_lock_again_on_the_same_thread()
    {
        rig.Repository.Locked(_ => rig.Repository.Locked(_ => rig.Note("inner")));

        Assert.Equal(["inner"], rig.Steps);
    }

    [Fact]
    public async Task Makes_another_threads_locked_action_wait_until_the_first_returns()
    {
        Task? other = null;

        rig.Repository.Locked(_ =>
        {
            other = rig.LockOnAnotherThread("other");
            rig.Note("first done");
        });

        await other!.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        Assert.Equal(["first done", "other"], rig.Steps);
    }

    [Fact]
    public async Task Makes_another_threads_locked_call_wait_until_the_first_call_returns()
    {
        Task? other = null;

        var result = rig.Repository.Locked(_ =>
        {
            other = rig.LockOnAnotherThread("other");
            rig.Note("first done");
            return 1;
        });

        await other!.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        Assert.Equal(1, result);
        Assert.Equal(["first done", "other"], rig.Steps);
    }

    [Fact]
    public void Raises_a_local_save_once_on_the_calling_thread()
    {
        var raisedOn = new List<int>();
        rig.Repository.LocalSaved += (_, _) => raisedOn.Add(Environment.CurrentManagedThreadId);

        rig.Repository.ReportLocalSave();

        Assert.Equal([Environment.CurrentManagedThreadId], raisedOn);
    }

    [Fact]
    public void Raises_a_local_save_for_each_report()
    {
        var raised = 0;
        rig.Repository.LocalSaved += (_, _) => raised++;

        rig.Repository.ReportLocalSave();
        rig.Repository.ReportLocalSave();

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Refuses_to_report_a_local_save_under_the_lock_and_raises_nothing()
    {
        var raised = 0;
        rig.Repository.LocalSaved += (_, _) => raised++;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Locked(_ => rig.Repository.ReportLocalSave()));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Never_raises_a_local_save_for_a_selection_or_an_update()
    {
        rig.Add("a");
        var raised = 0;
        rig.Repository.LocalSaved += (_, _) => raised++;

        rig.Repository.Select(Clone);
        rig.Repository.Update(Clone, () => { });
        rig.Repository.Select(null);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Refuses_a_selection_under_the_lock_before_doing_anything()
    {
        var part = rig.Add("a");

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Locked(_ => rig.Repository.Select(Clone)));

        Assert.Empty(rig.Steps);
        Assert.Null(rig.Repository.Folder);
        Assert.Equal(0, part.Notices);
    }

    [Fact]
    public void Refuses_an_update_under_the_lock_before_doing_anything()
    {
        var part = rig.Add("a");
        rig.Repository.Select(Clone);
        rig.ForgetSteps();
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Locked(_ => rig.Repository.Update(Clone, () => ran = true)));

        Assert.False(ran);
        Assert.Empty(rig.Steps);
        Assert.Equal(1, part.Notices);
    }
}
