using System.ComponentModel;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.KeepAwake.Tests;

/// <summary>Starting Keep awake: it reads the setting and the lock state, and holds the request only when the setting is on and the session unlocked.</summary>
[Unit]
public class KeepAwakeStartTests
{
    private static readonly Win32Exception Refusal = new(5, "Access refused by Windows.");

    [Fact]
    public void Is_off_and_holds_no_request_when_the_file_is_missing()
    {
        var app = new KeepAwakeHarness();

        app.App.Start();

        Assert.Equal(new KeepAwakeState(false, false, null), app.App.State);
        Assert.False(app.Display.IsSet);
        Assert.Empty(app.Display.SetCalls);
    }

    [Fact]
    public void Holds_the_request_when_the_file_says_on_and_the_session_is_unlocked()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");

        app.App.Start();

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Information));
        app.Log.Events.AssertNoProblems();
    }

    [Fact]
    public void Holds_no_request_when_the_file_says_off()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("off");

        app.App.Start();

        Assert.Equal(new KeepAwakeState(false, false, null), app.App.State);
        Assert.False(app.Display.IsSet);
    }

    [Fact]
    public void Starts_paused_and_holds_no_request_when_the_session_is_locked()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Session.Locked = true;

        app.App.Start();

        Assert.Equal(new KeepAwakeState(true, true, null), app.App.State);
        Assert.False(app.Display.IsSet);
        Assert.Empty(app.Display.SetCalls);
    }

    [Fact]
    public void Starts_off_not_paused_when_the_setting_is_off_and_the_session_is_locked()
    {
        var app = new KeepAwakeHarness();
        app.Session.Locked = true;

        app.App.Start();

        Assert.Equal(new KeepAwakeState(false, false, null), app.App.State);
    }

    [Theory]
    [InlineData("sometimes")]
    [InlineData("")]
    public void Counts_a_file_holding_something_else_as_off_with_the_reason_in_State_and_a_Warning(string content)
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting(content);

        app.App.Start();

        Assert.False(app.App.State.IsOn);
        Assert.False(string.IsNullOrWhiteSpace(app.App.State.Failure));
        Assert.False(app.Display.IsSet);
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Warning));
    }

    [Fact]
    public void Counts_an_unreadable_file_as_off_with_the_reason_in_State_and_a_Warning()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Files.FailReadsUnder(KeepAwakeHarness.DeviceFolder);

        app.App.Start();

        Assert.False(app.App.State.IsOn);
        Assert.False(string.IsNullOrWhiteSpace(app.App.State.Failure));
        Assert.False(app.Display.IsSet);
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Warning));
    }

    [Fact]
    public void Counts_a_lock_state_Windows_cannot_give_as_unlocked_and_logs_a_Warning()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Session.Failure = new Win32Exception(5, "Cannot say.");

        app.App.Start();

        Assert.True(app.App.State.IsOn);
        Assert.False(app.App.State.Paused);
        Assert.True(app.Display.IsSet);
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Warning));
    }

    [Fact]
    public void Shows_a_refused_request_in_State_keeps_the_setting_on_and_logs_an_Error_with_the_exception()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Display.Failure = Refusal;

        app.App.Start();

        Assert.True(app.App.State.IsOn);
        Assert.False(app.App.State.Paused);
        Assert.Contains(Refusal.Message, app.App.State.Failure);
        Assert.False(app.Display.IsSet);
        Assert.Equal("on", app.SavedSetting());
        Assert.True(app.Log.Events.SingleAt(LogEventLevel.Error).HasException(Refusal));
    }
}
