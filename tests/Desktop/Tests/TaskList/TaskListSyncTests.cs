using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListSyncTests
{
    [WpfFact]
    public void Opening_Task_View_starts_sync_and_its_button_and_F5_start_later_passes()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        Assert.Equal(1, services.Sync.SyncCount);

        screen.Click("Sync");
        Assert.Equal(2, services.Sync.SyncCount);
        screen.Press(Key.F5);
        Assert.Equal(3, services.Sync.SyncCount);
    }

    [WpfFact]
    public async Task A_pending_sync_does_not_delay_the_window_or_start_again_on_F5()
    {
        var services = new ListServices();
        services.Add("Local task");
        var pending = new TaskCompletionSource<ISyncStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Sync.SyncOperation = _ => pending.Task;
        using var screen = new TaskListScreen(services);
        try
        {
            Assert.True(screen.View.IsOpen);
            Assert.Equal(["Local task"], screen.RowTitles);
            screen.Press(Key.F5);
            Assert.Equal(1, services.Sync.SyncCount);
            services.Add("Still editable");
            Assert.Contains("Still editable", screen.RowTitles);
        }
        finally
        {
            pending.TrySetResult(new FakeSyncStatus());
        }
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    [WpfFact]
    public async Task While_the_opening_sync_runs_the_Sync_button_is_green_and_off_with_Syncing_after_it_and_returns_to_normal_when_it_ends()
    {
        var services = new ListServices();
        var pending = new TaskCompletionSource<ISyncStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Sync.SyncOperation = _ => pending.Task;
        using var screen = new TaskListScreen(services);
        screen.UseTheme();
        try
        {
            var running = screen.SyncButton;
            Assert.False(running.IsEnabled);
            Assert.Equal(screen.ThemeColor("SystemFillColorSuccessBrush"), screen.SyncButtonFill);
            var line = screen.FailureLine("Syncing…");
            Assert.NotNull(line);
            Assert.Equal("Syncing…", OpenWindows.TextOf(line));
            Assert.True(screen.IsInStatusBar(line));
            Assert.InRange(screen.LeftOf(line) - screen.RightOf(running), -0.5, 24);
        }
        finally
        {
            pending.TrySetResult(new FakeSyncStatus());
        }

        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        screen.Settle();

        Assert.True(screen.SyncButton.IsEnabled);
        Assert.NotEqual(screen.ThemeColor("SystemFillColorSuccessBrush"), screen.SyncButtonFill);
        Assert.Null(screen.FailureLine("Syncing…"));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_sync_started_by_the_button_or_F5_turns_the_Sync_button_green_and_off_while_it_runs(bool byKey)
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        screen.UseTheme();
        var pending = new TaskCompletionSource<ISyncStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Sync.SyncOperation = _ => pending.Task;
        try
        {
            if (byKey)
            {
                screen.Press(Key.F5);
            }
            else
            {
                screen.Click("Sync");
            }

            Assert.False(screen.SyncButton.IsEnabled);
            Assert.Equal(screen.ThemeColor("SystemFillColorSuccessBrush"), screen.SyncButtonFill);
            Assert.NotNull(screen.FailureLine("Syncing…"));
        }
        finally
        {
            pending.TrySetResult(new FakeSyncStatus());
        }

        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        screen.Settle();
        Assert.True(screen.SyncButton.IsEnabled);
    }

    [WpfFact]
    public void A_sync_that_starts_on_its_own_turns_the_Sync_button_green_and_off_and_F5_starts_no_second_sync()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        screen.UseTheme();
        Assert.Equal(1, services.Sync.SyncCount);

        screen.PublishSync(SyncState.Working);

        Assert.False(screen.SyncButton.IsEnabled);
        Assert.Equal(screen.ThemeColor("SystemFillColorSuccessBrush"), screen.SyncButtonFill);
        Assert.NotNull(screen.FailureLine("Syncing…"));
        screen.Press(Key.F5);
        Assert.Equal(1, services.Sync.SyncCount);

        screen.PublishSync(SyncState.Idle);

        Assert.True(screen.SyncButton.IsEnabled);
        Assert.Null(screen.FailureLine("Syncing…"));
        screen.Press(Key.F5);
        Assert.Equal(2, services.Sync.SyncCount);
    }

    [WpfFact]
    public void A_failed_manual_sync_shows_its_details_beside_the_button()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        services.Sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Connection, Details = "Requested sync failed"
        });

        screen.Click("Sync");

        Assert.True(screen.ShowsText("Requested sync failed"));
    }

    [WpfFact]
    public async Task Tasks_arriving_on_a_worker_thread_appear_without_disturbing_an_open_edit()
    {
        var services = new ListServices();
        services.Add("Editing");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Editing", Key.F2);
        screen.TypeDescription("Unsaved work");

        await Task.Run(() => services.Add("Arrived from sync"));
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.Contains("Arrived from sync", screen.RowTitles);
        Assert.Equal("Unsaved work", screen.EditedDescriptionText);
    }
}
