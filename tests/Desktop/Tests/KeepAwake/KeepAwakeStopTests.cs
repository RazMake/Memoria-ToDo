using System.ComponentModel;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.KeepAwake.Tests;

/// <summary>Stopping Keep awake: it clears the request and stops following locks, and a repeat does nothing.</summary>
[Unit]
public class KeepAwakeStopTests
{
    [Fact]
    public void Clears_the_request()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();

        app.App.Stop();

        Assert.False(app.Display.IsSet);
    }

    [Fact]
    public void Logs_the_request_cleared_at_Information()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        var before = app.Log.Events.At(LogEventLevel.Information).Count;

        app.App.Stop();

        Assert.True(app.Log.Events.At(LogEventLevel.Information).Count > before);
        app.Log.Events.AssertNoProblems();
    }

    [Fact]
    public void Keeps_the_saved_setting()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();

        app.App.Stop();

        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void Stops_following_locks()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Session.Locked = true;
        app.App.Start();
        app.App.Stop();

        app.Session.Unlock();

        Assert.False(app.Display.IsSet);
        Assert.Empty(app.Display.SetCalls);
    }

    [Fact]
    public void A_repeated_stop_does_nothing()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        app.App.Stop();
        var calls = app.Display.SetCalls.Count;
        var entries = app.Log.Events.Count;

        app.App.Stop();

        Assert.Equal(calls, app.Display.SetCalls.Count);
        Assert.Equal(entries, app.Log.Events.Count);
    }

    [Fact]
    public void A_request_that_cannot_be_cleared_logs_an_Error_with_the_exception_and_throws()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        var refusal = new Win32Exception(5, "Access refused by Windows.");
        app.Display.Failure = refusal;

        Assert.ThrowsAny<Exception>(app.App.Stop);

        Assert.True(app.Log.Events.SingleAt(LogEventLevel.Error).HasException(refusal));
    }
}
