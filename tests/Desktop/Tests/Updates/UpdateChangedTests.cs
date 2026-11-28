using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>Changed: raised on the UI thread, after each change of the state. The test's own thread plays the UI thread.</summary>
[Unit]
public class UpdateChangedTests
{
    [WpfFact]
    public async Task Is_raised_on_the_UI_thread_when_a_check_ends_on_another_thread()
    {
        var uiThread = Environment.CurrentManagedThreadId;
        var app = new UpdatesHarness();
        app.Install.HoldChecks();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");
        var raisedOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Updates.Changed += (_, _) => raisedOn.TrySetResult(Environment.CurrentManagedThreadId);
        app.Start();
        await app.Install.WaitForChecksAsync(1);

        await Task.Run(app.Install.ReleaseChecks);

        Assert.Equal(uiThread, await raisedOn.Task.WaitAsync(Shared.Fakes.TestWait.ForSignal));
    }

    [WpfFact]
    public async Task Is_raised_after_the_state_changed()
    {
        var app = new UpdatesHarness();
        var stages = new List<UpdateStage>();
        app.Updates.Changed += (_, _) => stages.Add(app.Updates.State.Stage);
        app.Install.Found = UpdatesHarness.Newer("2.2.0");

        app.Start();
        await app.WaitForStateAsync(s => s.Stage == UpdateStage.Available);

        Assert.Equal(UpdateStage.Available, stages.First());
    }

    [WpfFact]
    public async Task Is_raised_for_each_step_of_a_failed_update()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        var stages = new List<UpdateStage>();
        app.Updates.Changed += (_, _) => stages.Add(app.Updates.State.Stage);
        app.Install.FailDownloads(new HttpRequestException("No network."));

        app.Updates.UpdateNow();
        await app.WaitForStateAsync(s => s.Failure is not null);

        var steps = stages.Where((stage, i) => i == 0 || stage != stages[i - 1]).ToList();
        Assert.Equal([UpdateStage.Updating, UpdateStage.Available], steps);
    }
}
