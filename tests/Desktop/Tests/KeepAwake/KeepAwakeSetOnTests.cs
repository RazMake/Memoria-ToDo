using System.ComponentModel;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.KeepAwake.Tests;

/// <summary>Turning Prevent screen saver on and off with SetOn: it applies at once and is saved on this device.</summary>
[Unit]
public class KeepAwakeSetOnTests
{
    private static readonly Win32Exception Refusal = new(5, "Access refused by Windows.");

    private static int Informations(KeepAwakeHarness app) => app.Log.Events.At(LogEventLevel.Information).Count;

    [Fact]
    public void Turning_it_on_holds_the_request_and_saves_on()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();

        app.App.SetOn(true);

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void Turning_it_on_before_Start_only_saves_the_setting_and_asks_Windows_for_nothing()
    {
        var app = new KeepAwakeHarness();

        app.App.SetOn(true);

        Assert.Empty(app.Display.SetCalls);
        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void Turning_it_on_after_Stop_only_saves_the_setting_and_does_not_set_the_request_again()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();
        app.App.Stop();

        app.App.SetOn(true);

        Assert.Empty(app.Display.SetCalls);
        Assert.False(app.Display.IsSet);
        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void Turning_it_off_clears_the_request_and_saves_off()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();

        app.App.SetOn(false);

        Assert.Equal(new KeepAwakeState(false, false, null), app.App.State);
        Assert.False(app.Display.IsSet);
        Assert.Equal("off", app.SavedSetting());
    }

    [Fact]
    public void Turning_it_on_while_the_session_is_locked_saves_on_and_holds_no_request()
    {
        var app = new KeepAwakeHarness();
        app.Session.Locked = true;
        app.App.Start();

        app.App.SetOn(true);

        Assert.Equal(new KeepAwakeState(true, true, null), app.App.State);
        Assert.False(app.Display.IsSet);
        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void Raises_Changed_after_State_changed()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();
        var seen = new List<KeepAwakeState>();
        app.App.Changed += (_, _) => seen.Add(app.App.State);

        app.App.SetOn(true);
        app.App.SetOn(false);

        Assert.Equal([new KeepAwakeState(true, false, null), new KeepAwakeState(false, false, null)], seen);
    }

    [Fact]
    public void Logs_the_setting_turned_on_and_off_at_Information()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();
        var before = Informations(app);

        app.App.SetOn(true);
        var afterOn = Informations(app);
        app.App.SetOn(false);

        Assert.True(afterOn > before);
        Assert.True(Informations(app) > afterOn);
        app.Log.Events.AssertNoProblems();
    }

    [Fact]
    public void A_failed_save_throws_and_keeps_the_previous_value_off()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();
        var changes = app.CountChanges();
        app.Files.FailWritesUnder(KeepAwakeHarness.DeviceFolder);

        Assert.ThrowsAny<Exception>(() => app.App.SetOn(true));

        Assert.Equal(new KeepAwakeState(false, false, null), app.App.State);
        Assert.False(app.Display.IsSet);
        Assert.False(app.Files.FileExists(KeepAwakeHarness.SettingFile));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void A_failed_save_throws_and_keeps_the_previous_value_on_and_the_request_held()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        app.Files.FailWritesUnder(KeepAwakeHarness.DeviceFolder);

        Assert.ThrowsAny<Exception>(() => app.App.SetOn(false));

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void A_refused_request_does_not_fail_the_call_and_shows_in_State_with_an_Error()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();
        app.Display.Failure = Refusal;

        app.App.SetOn(true);

        Assert.True(app.App.State.IsOn);
        Assert.Contains(Refusal.Message, app.App.State.Failure);
        Assert.Equal("on", app.SavedSetting());
        Assert.True(app.Log.Events.SingleAt(LogEventLevel.Error).HasException(Refusal));
    }

    [Fact]
    public void A_request_that_cannot_be_cleared_does_not_fail_the_call_and_shows_in_State_with_an_Error()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        app.Display.Failure = Refusal;

        app.App.SetOn(false);

        Assert.Contains(Refusal.Message, app.App.State.Failure);
        Assert.Equal("off", app.SavedSetting());
        Assert.True(app.Log.Events.SingleAt(LogEventLevel.Error).HasException(Refusal));
    }

    [Fact]
    public void Turning_it_off_and_on_again_retries_a_refused_request()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Display.Failure = Refusal;
        app.App.Start();
        app.Display.Failure = null;

        app.App.SetOn(false);
        app.App.SetOn(true);

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
    }

    [Fact]
    public void Turning_it_on_after_a_damaged_file_writes_the_file_again_and_clears_the_failure()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("sometimes");
        app.App.Start();

        app.App.SetOn(true);

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void The_saved_setting_is_read_by_the_next_run()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();
        app.App.SetOn(true);
        var next = app.Create();

        next.Start();

        Assert.True(next.State.IsOn);
    }
}
