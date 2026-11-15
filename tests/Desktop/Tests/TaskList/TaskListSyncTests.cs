using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
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
