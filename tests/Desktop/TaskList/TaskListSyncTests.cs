using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
public class TaskListSyncTests
{
    [WpfFact]
    public void Opening_View_Tasks_starts_sync_and_its_button_and_F5_start_later_passes()
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

    [WpfTheory]
    [InlineData(SyncState.Error)]
    [InlineData(SyncState.Offline)]
    public void A_failed_manual_sync_shows_its_details_beside_the_button(SyncState state)
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        services.Sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus
        {
            State = state, Failure = SyncFailure.Connection, Details = "Requested sync failed"
        });

        screen.Click("Sync");

        Assert.True(screen.ShowsText("Requested sync failed"));
    }

    [WpfFact]
    public void Closing_View_Tasks_cancels_its_wait_not_the_shared_pass()
    {
        var services = new ListServices();
        var pass = new TaskCompletionSource<ISyncStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken caller = default;
        services.Sync.SyncOperation = token =>
        {
            caller = token;
            return pass.Task.WaitAsync(token);
        };
        using var screen = new TaskListScreen(services);

        screen.Window.Close();

        Assert.True(caller.IsCancellationRequested);
        Assert.False(pass.Task.IsCompleted);
        pass.SetResult(new FakeSyncStatus());
    }

    [WpfFact]
    public async Task A_sync_request_during_app_shutdown_finishes_quietly()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        services.Sync.SyncOperation = _ => Task.FromException<ISyncStatus>(
            new ObjectDisposedException("Sync service"));

        screen.Click("Sync");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.True(screen.View.IsOpen);
        Assert.False(screen.ShowsText("Sync service"));
    }

    [WpfFact]
    public async Task An_unexpected_exception_from_the_sync_started_on_opening_is_shown_beside_the_button()
    {
        var services = new ListServices();
        services.Add("Local task");
        services.Sync.SyncOperation = _ => Task.FromException<ISyncStatus>(new IOException("Unexpected backend failure"));
        using var screen = new TaskListScreen(services);

        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.True(screen.ShowsText("Unexpected backend failure"));
        Assert.True(screen.View.IsOpen);
        Assert.Equal(["Local task"], screen.RowTitles);
    }

    [WpfFact]
    public async Task An_unexpected_exception_from_a_manual_sync_is_shown_beside_the_button_and_the_window_stays_usable()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        services.Sync.SyncOperation = _ => Task.FromException<ISyncStatus>(new IOException("Unexpected backend failure"));

        screen.Click("Sync");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        services.Add("Added afterwards");

        Assert.True(screen.ShowsText("Unexpected backend failure"));
        Assert.True(screen.View.IsOpen);
        Assert.Contains("Added afterwards", screen.RowTitles);
    }

    [WpfFact]
    public async Task The_Sync_button_shows_that_a_requested_pass_is_running_and_returns_when_it_ends()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        Assert.Equal("Sync", screen.SyncButtonText);
        var pass = new TaskCompletionSource<ISyncStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Sync.SyncOperation = _ => pass.Task;

        screen.Click("Sync");

        Assert.Equal("Syncing…", screen.SyncButtonText);
        var buttonReturned = screen.SyncButtonShowsAsync("Sync", TimeSpan.FromSeconds(15));
        pass.SetResult(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Connection, Details = "Pass ended badly" });
        await buttonReturned;
        Assert.Equal("Sync", screen.SyncButtonText);
        Assert.True(screen.ShowsText("Pass ended badly"));
    }

    [WpfFact]
    public async Task Tasks_arriving_on_a_worker_thread_appear_without_disturbing_an_open_edit()
    {
        var services = new ListServices();
        services.Add("Editing");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Editing", Key.F2);
        screen.TypeTitle("Editing", "Unsaved work");

        await Task.Run(() => services.Add("Arrived from sync"));
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.Contains("Arrived from sync", screen.RowTitles);
        Assert.Equal("Unsaved work", screen.EditorTitleText("Editing"));
    }
}
