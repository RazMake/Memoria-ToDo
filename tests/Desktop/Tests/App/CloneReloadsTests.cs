using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The app reloads the selected clone half a second after the last change to a file in it, outside .git.</summary>
[Unit]
public sealed class CloneReloadsTests : IDisposable
{
    private const string Clone = CloneReloadsRig.Clone;
    private const string Other = CloneReloadsRig.Other;
    private static readonly TimeSpan Wait = CloneReloadsRig.Wait;
    private static readonly TimeSpan Millisecond = TimeSpan.FromMilliseconds(1);

    private readonly CloneReloadsRig r = new();

    public void Dispose() => r.Dispose();

    private FakeFolderWatcher.Watch WatchClone()
    {
        r.Reloads.Watch(Clone);
        return r.Watcher.Watches[^1];
    }

    private void Elapse(TimeSpan by) => r.Time.Advance(by);

    [Fact]
    public void A_change_reloads_the_repository_once_after_the_wait_and_not_before()
    {
        var watch = WatchClone();

        watch.Changed("autocomplete/contacts/team.json");
        Elapse(Wait - Millisecond);
        Assert.Equal(0, r.Loads);
        Elapse(Millisecond);

        Assert.Equal(1, r.Loads);
        Assert.Equal(1, r.Notices);
        Elapse(TimeSpan.FromSeconds(5));
        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void A_burst_of_changes_reloads_once_after_the_wait_that_follows_the_last_change()
    {
        var watch = WatchClone();

        watch.Changed("a.json");
        Elapse(TimeSpan.FromMilliseconds(300));
        watch.Changed("b.json");
        Elapse(TimeSpan.FromMilliseconds(300));
        watch.Changed("c.json");
        Elapse(Wait - Millisecond);
        Assert.Equal(0, r.Loads);
        Elapse(Millisecond);

        Assert.Equal(1, r.Loads);
        Elapse(TimeSpan.FromSeconds(5));
        Assert.Equal(1, r.Loads);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(@".git\index")]
    [InlineData(@".GIT\HEAD")]
    [InlineData(@".Git\objects\ab\cdef")]
    public void Changes_to_the_git_folder_or_a_file_in_it_do_not_reload(string path)
    {
        var watch = WatchClone();

        watch.Changed(path);
        Elapse(TimeSpan.FromSeconds(5));

        Assert.Equal(0, r.Loads);
        Assert.Equal(0, r.Notices);
    }

    [Theory]
    [InlineData(@".github\workflows\x.yml")]
    [InlineData(@"a\.git\y")]
    [InlineData(@"notes.git\y")]
    public void A_change_to_a_path_that_only_looks_like_the_git_folder_reloads(string path)
    {
        var watch = WatchClone();

        watch.Changed(path);
        Elapse(Wait);

        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void Watching_another_folder_stops_the_old_watch_starts_one_on_the_new_folder_and_cancels_the_reload_waiting_for_the_old_one()
    {
        var first = WatchClone();
        first.Changed("a.json");
        Elapse(TimeSpan.FromMilliseconds(300));

        r.Reloads.Watch(Other);
        r.Repository.Select(Other);
        r.ForgetLoads();
        Elapse(TimeSpan.FromSeconds(5));

        Assert.True(first.IsDisposed);
        Assert.Equal([Clone, Other], r.Watcher.Folders);
        Assert.False(r.Watcher.Watches[1].IsDisposed);
        Assert.Equal(0, r.Loads);
    }

    [Fact]
    public void A_change_after_watching_another_folder_reloads_that_folder()
    {
        WatchClone();
        r.Reloads.Watch(Other);
        r.Repository.Select(Other);
        r.ForgetLoads();

        r.Watcher.Watches[1].Changed("a.json");
        Elapse(Wait);

        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void Watching_no_folder_stops_the_watch_and_cancels_a_waiting_reload()
    {
        var watch = WatchClone();
        watch.Changed("a.json");
        Elapse(TimeSpan.FromMilliseconds(300));

        r.Reloads.Watch(null);
        Elapse(TimeSpan.FromSeconds(5));

        Assert.True(watch.IsDisposed);
        Assert.Equal(0, r.Loads);
        Assert.Equal([Clone], r.Watcher.Folders);
    }

    [Fact]
    public void An_overflow_of_the_watcher_s_buffer_reloads_after_the_same_wait()
    {
        var watch = WatchClone();

        watch.Overflowed();
        Elapse(Wait - Millisecond);
        Assert.Equal(0, r.Loads);
        Elapse(Millisecond);

        Assert.Equal(1, r.Loads);
        Assert.Equal(1, r.Notices);
    }

    [Fact]
    public void A_failed_watch_logs_a_warning_reloads_once_and_stops_the_watch()
    {
        var watch = WatchClone();
        var before = r.Logs.Events.Count;

        watch.Failed(new IOException("The watcher broke."));
        Elapse(Wait);

        r.Logs.After(before).SingleAt(LogEventLevel.Warning, Clone);
        Assert.Equal(1, r.Loads);
        Assert.True(watch.IsDisposed);
        watch.Changed("a.json");
        Elapse(TimeSpan.FromSeconds(5));
        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void Watching_a_folder_again_after_a_failed_watch_watches_it_again()
    {
        var failed = WatchClone();
        failed.Failed(new IOException("The watcher broke."));
        Elapse(Wait);
        r.ForgetLoads();

        var again = WatchClone();
        again.Changed("a.json");
        Elapse(Wait);

        Assert.NotSame(failed, again);
        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void A_watch_that_can_not_start_logs_a_warning_without_throwing_and_reloads_nothing()
    {
        r.Watcher.FailToStart = new IOException("Too many watchers.");
        var before = r.Logs.Events.Count;

        var thrown = Record.Exception(() => r.Reloads.Watch(Clone));

        Assert.Null(thrown);
        r.Logs.After(before).SingleAt(LogEventLevel.Warning, Clone);
        Elapse(TimeSpan.FromSeconds(5));
        Assert.Equal(0, r.Loads);
    }

    [Fact]
    public void A_reload_that_fails_logs_an_error_without_throwing_and_the_next_change_reloads_again()
    {
        var watch = WatchClone();
        r.Part.FailToLoad = new IOException("The disk failed.");
        var before = r.Logs.Events.Count;
        watch.Changed("a.json");

        var thrown = Record.Exception(() => Elapse(Wait));

        Assert.Null(thrown);
        r.Logs.After(before).SingleAt(LogEventLevel.Error, Clone);
        r.Part.FailToLoad = null;
        watch.Changed("a.json");
        Elapse(Wait);
        Assert.Equal(1, r.Loads);
        Assert.Equal(1, r.Notices);
    }

    [Fact]
    public void Disposing_cancels_a_waiting_reload_and_stops_the_watch()
    {
        var watch = WatchClone();
        watch.Changed("a.json");
        Elapse(TimeSpan.FromMilliseconds(300));

        r.Reloads.Dispose();
        Elapse(TimeSpan.FromSeconds(5));

        Assert.True(watch.IsDisposed);
        Assert.Equal(0, r.Loads);
    }

    [Fact]
    public void A_reload_reports_no_local_save()
    {
        var watch = WatchClone();

        watch.Changed("a.json");
        Elapse(Wait);

        Assert.Equal(1, r.Loads);
        Assert.Equal(0, r.LocalSaves);
    }

    [Fact]
    public void A_reload_for_a_folder_no_longer_selected_does_nothing_and_logs_no_error()
    {
        var watch = WatchClone();
        watch.Changed("a.json");
        r.Repository.Select(Other);
        r.ForgetLoads();
        var before = r.Logs.Events.Count;

        Elapse(Wait);

        Assert.Equal(0, r.Loads);
        Assert.Equal(0, r.Notices);
        Assert.DoesNotContain(r.Logs.After(before), entry => entry.Level >= LogEventLevel.Error);
    }

    [Fact]
    public void Watching_a_folder_logs_information_naming_it()
    {
        var before = r.Logs.Events.Count;

        r.Reloads.Watch(Clone);

        r.Logs.After(before).SingleAt(LogEventLevel.Information, Clone);
    }

    [Fact]
    public void Watching_no_folder_logs_information()
    {
        WatchClone();
        var before = r.Logs.Events.Count;

        r.Reloads.Watch(null);

        r.Logs.After(before).SingleAt(LogEventLevel.Information);
    }

    [Fact]
    public void A_timer_call_that_comes_before_the_wait_after_the_last_change_has_passed_reloads_nothing_and_the_reload_follows_the_real_wait()
    {
        var time = new ManualTimerTime();
        using var reloads = new CloneReloads(r.Watcher, r.Repository, time, r.Logs.CreateLogger());
        reloads.Watch(Clone);
        var watch = r.Watcher.Watches[^1];
        r.ForgetLoads();

        watch.Changed("a.json");
        time.Move(Wait);
        watch.Changed("b.json");
        time.Fire();

        Assert.Equal(0, r.Loads);
        time.Move(Wait);
        time.Fire();
        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void A_change_made_in_the_new_folder_while_a_selection_reads_it_reloads_it()
    {
        WatchClone();
        r.Part.OnLoad = folder =>
        {
            if (folder != Other)
                return;
            r.Part.OnLoad = null;
            var newest = r.Watcher.Watches[^1];
            if (newest.Folder == Other)
                newest.Changed("a.json");
        };

        r.Reloads.Select(Other, r.Repository.Select);
        r.ForgetLoads();
        Elapse(Wait);

        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void A_reload_that_comes_while_a_selection_still_reads_waits_for_it_and_then_reloads_the_new_folder()
    {
        using var entered = new ManualResetEventSlim();
        using var updateEntered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var advanced = new ManualResetEventSlim();
        using var rig = new CloneReloadsRig(repository => new InterceptedRepository(repository, _ => updateEntered.Set()));
        rig.Reloads.Watch(Other);
        var reading = 0;
        rig.Part.OnLoad = folder =>
        {
            if (folder != Other || Interlocked.Exchange(ref reading, 1) != 0)
                return;
            entered.Set();
            release.Wait(TestWait.ForSignal, TestContext.Current.CancellationToken);
        };
        Exception? selectFailure = null;
        var selecting = new Thread(() =>
        {
            try { rig.Reloads.Select(Other, rig.Repository.Select); }
            catch (Exception e) { selectFailure = e; }
        });
        Exception? advanceFailure = null;
        var advancing = new Thread(() =>
        {
            try
            {
                rig.Watcher.Watches[^1].Changed("a.json");
                rig.Time.Advance(Wait);
            }
            catch (Exception e) { advanceFailure = e; }
            finally { advanced.Set(); }
        });

        selecting.Start();
        Assert.True(entered.Wait(TestWait.ForSignal, TestContext.Current.CancellationToken));
        Assert.Equal(Other, rig.Watcher.Watches[^1].Folder);
        advancing.Start();
        WaitHandle.WaitAny([updateEntered.WaitHandle, advanced.WaitHandle], TestWait.ForSignal);
        release.Set();
        Assert.True(selecting.Join(TestWait.ForSignal));
        Assert.True(advancing.Join(TestWait.ForSignal));

        Assert.Null(selectFailure);
        Assert.Null(advanceFailure);
        Assert.Equal(Other, rig.Repository.Folder);
        Assert.Equal(2, rig.Loads);
    }

    [Fact]
    public void A_selection_whose_notice_fails_throws_and_leaves_the_watch_on_the_folder_the_repository_holds()
    {
        var clone = WatchClone();
        r.Part.FailToNotify = new IOException("A notice failed.");

        var thrown = Record.Exception(() => r.Reloads.Select(Other, r.Repository.Select));

        Assert.IsType<IOException>(thrown);
        Assert.Equal(Other, r.Repository.Folder);
        Assert.True(clone.IsDisposed);
        var active = Assert.Single(r.Watcher.Watches, watch => !watch.IsDisposed);
        Assert.Equal(Other, active.Folder);
    }

    [Fact]
    public void A_selection_whose_read_fails_throws_and_keeps_watching_the_folder_the_repository_keeps()
    {
        WatchClone();
        r.Part.FailToLoad = new IOException("The read failed.");

        var thrown = Record.Exception(() => r.Reloads.Select(Other, r.Repository.Select));

        Assert.IsType<IOException>(thrown);
        Assert.Equal(Clone, r.Repository.Folder);
        var active = Assert.Single(r.Watcher.Watches, watch => !watch.IsDisposed);
        Assert.Equal(Clone, active.Folder);
        r.Part.FailToLoad = null;
        r.ForgetLoads();
        active.Changed("a.json");
        Elapse(Wait);
        Assert.Equal(1, r.Loads);
    }

    [Fact]
    public void A_reload_the_repository_rejects_because_the_folder_is_no_longer_selected_logs_no_error_and_reloads_nothing()
    {
        using var rig = new CloneReloadsRig(repository => new InterceptedRepository(repository, _ => repository.Select(Other)));
        rig.Reloads.Watch(Clone);
        rig.ForgetLoads();
        var before = rig.Logs.Events.Count;
        rig.Watcher.Watches[^1].Changed("a.json");

        var thrown = Record.Exception(() => rig.Time.Advance(Wait));

        Assert.Null(thrown);
        Assert.DoesNotContain(rig.Logs.After(before), entry => entry.Level >= LogEventLevel.Error);
        Assert.Equal(1, rig.Loads);
        Assert.Equal(Other, rig.Repository.Folder);
    }
}
