using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 10, syncing: opening Task View starts a sync, the Sync button and F5 start later ones, a failed sync shows its details
/// beside the button, and tasks a sync adds appear without disturbing an open edit. How a sync's state becomes the status bar's
/// text is in <see cref="StatusBarTextTests"/>.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewSyncPathTests
{
    [WpfFact]
    public async Task Syncing_on_open_by_button_and_by_F5_shows_a_failure_and_adds_tasks_beside_an_open_edit()
    {
        var services = new ListServices();
        services.Add("Editing");
        using var screen = new TaskListScreen(services);
        Assert.Equal(1, services.Sync.SyncCount);

        screen.Click("Sync");
        Assert.Equal(2, services.Sync.SyncCount);
        screen.Press(Key.F5);
        Assert.Equal(3, services.Sync.SyncCount);

        services.Sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Connection, Details = "Requested sync failed"
        });
        screen.Click("Sync");
        Assert.True(screen.ShowsText("Requested sync failed"));

        screen.PressOn("Editing", Key.F2);
        screen.TypeDescription("Unsaved work");
        await Task.Run(() => services.Add("Arrived from sync"), TestContext.Current.CancellationToken);
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.Contains("Arrived from sync", screen.RowTitles);
        Assert.Equal("Unsaved work", screen.EditedDescriptionText);
    }
}
