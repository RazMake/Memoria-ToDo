using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>A copy run from a build folder shows its version and never updates. Waits run on the UI thread, which these tests' own thread plays.</summary>
[Unit]
public class NotInstalledTests
{
    private static UpdatesHarness NotInstalled()
    {
        var app = new UpdatesHarness();
        app.Install.IsInstalled = false;
        app.Install.Version = "2.0.0-dev";
        app.Install.Found = UpdatesHarness.Newer("9.9.9");
        return app;
    }

    [WpfFact]
    public void State_shows_the_running_version_and_no_new_one()
    {
        var app = NotInstalled();

        app.Start();

        Assert.Equal(new UpdateState(UpdateStage.NotInstalled, "2.0.0-dev", null, null), app.Updates.State);
    }

    [WpfFact]
    public async Task Never_checks_at_start_or_at_night()
    {
        var app = NotInstalled();
        app.Start();

        app.Time.Advance(TimeSpan.FromDays(3));
        await UpdatesHarness.Pump();

        Assert.Equal(0, app.Install.CheckCount);
        Assert.Equal(UpdateStage.NotInstalled, app.Updates.State.Stage);
    }

    [WpfFact]
    public void Auto_update_is_off_even_when_the_file_says_on()
    {
        var app = NotInstalled();
        app.WriteAutoUpdateFile("on");

        app.Start();

        Assert.False(app.Updates.AutoUpdate);
    }

    [WpfFact]
    public void Turning_Auto_update_on_fails_and_saves_nothing()
    {
        var app = NotInstalled();
        app.Start();

        Assert.ThrowsAny<Exception>(() => app.Updates.SetAutoUpdate(true));

        Assert.False(app.Updates.AutoUpdate);
        Assert.False(app.Files.FileExists(UpdatesHarness.AutoUpdateFile));
    }

    [WpfFact]
    public void Update_now_does_nothing()
    {
        var app = NotInstalled();
        app.Start();

        app.Updates.UpdateNow();

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Equal(0, app.Exit.EndCount);
    }
}
