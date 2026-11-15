using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>Auto update: read from auto-update.txt in the device folder when Updates starts, and saved there when it changes.</summary>
[Unit]
public class AutoUpdateSettingTests
{
    private static string SavedText(UpdatesHarness app) =>
        Encoding.UTF8.GetString(app.Files.ReadFile(UpdatesHarness.AutoUpdateFile)).Trim();

    [WpfFact]
    public void Is_off_when_the_file_is_missing()
    {
        var app = new UpdatesHarness();

        app.Start();

        Assert.False(app.Updates.AutoUpdate);
        Assert.Null(app.Updates.State.Failure);
    }

    [WpfTheory]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void Reads_on_or_off_from_the_file(string text, bool expected)
    {
        var app = new UpdatesHarness();
        app.WriteAutoUpdateFile(text);

        app.Start();

        Assert.Equal(expected, app.Updates.AutoUpdate);
        Assert.Null(app.Updates.State.Failure);
    }

    [WpfFact]
    public void Is_off_with_a_failure_when_the_file_holds_something_else()
    {
        var app = new UpdatesHarness();
        app.WriteAutoUpdateFile("sometimes");

        app.Start();

        Assert.False(app.Updates.AutoUpdate);
        Assert.False(string.IsNullOrWhiteSpace(app.Updates.State.Failure));
    }

    [WpfFact]
    public void Is_off_with_a_failure_when_the_file_cannot_be_read()
    {
        var app = new UpdatesHarness();
        app.WriteAutoUpdateFile("on");
        app.Files.FailReadsUnder(UpdatesHarness.DeviceFolder);

        app.Start();

        Assert.False(app.Updates.AutoUpdate);
        Assert.False(string.IsNullOrWhiteSpace(app.Updates.State.Failure));
    }

    [WpfFact]
    public async Task Turning_it_on_saves_on_and_raises_Changed()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        var changed = new TaskCompletionSource();
        app.Updates.Changed += (_, _) => changed.TrySetResult();

        app.Updates.SetAutoUpdate(true);
        await changed.Task.WaitAsync(Shared.Fakes.TestWait.ForSignal);

        Assert.True(app.Updates.AutoUpdate);
        Assert.Equal("on", SavedText(app));
    }

    [WpfFact]
    public async Task Turning_it_off_saves_off()
    {
        var app = new UpdatesHarness();
        app.WriteAutoUpdateFile("on");
        await app.StartAndFirstCheckDoneAsync();

        app.Updates.SetAutoUpdate(false);

        Assert.False(app.Updates.AutoUpdate);
        Assert.Equal("off", SavedText(app));
    }

    [WpfFact]
    public async Task A_failed_save_keeps_the_old_value_and_throws()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        app.Files.FailWritesUnder(UpdatesHarness.DeviceFolder);

        Assert.ThrowsAny<Exception>(() => app.Updates.SetAutoUpdate(true));

        Assert.False(app.Updates.AutoUpdate);
        Assert.False(app.Files.FileExists(UpdatesHarness.AutoUpdateFile));
    }

    [WpfFact]
    public async Task Cannot_be_changed_while_updating()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        Assert.ThrowsAny<Exception>(() => app.Updates.SetAutoUpdate(true));

        Assert.False(app.Updates.AutoUpdate);
        Assert.False(app.Files.FileExists(UpdatesHarness.AutoUpdateFile));
    }

    [WpfFact]
    public async Task Turning_it_on_starts_no_update()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();

        app.Updates.SetAutoUpdate(true);
        await UpdatesHarness.Pump();

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Empty(app.Exit.Calls);
        Assert.Equal(UpdateStage.Available, app.Updates.State.Stage);
    }
}
