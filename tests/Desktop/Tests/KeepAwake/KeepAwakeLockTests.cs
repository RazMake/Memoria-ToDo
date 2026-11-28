using System.ComponentModel;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.KeepAwake.Tests;

/// <summary>Locks while Keep awake runs: any lock pauses it until the session is unlocked, and never changes the setting.</summary>
[Unit]
public class KeepAwakeLockTests
{
    private static readonly Win32Exception Refusal = new(5, "Access refused by Windows.");

    private static int Informations(KeepAwakeHarness app) => app.Log.Events.At(LogEventLevel.Information).Count;

    [Fact]
    public void A_lock_clears_the_request_and_pauses_it()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();

        app.Session.Lock();

        Assert.Equal(new KeepAwakeState(true, true, null), app.App.State);
        Assert.False(app.Display.IsSet);
    }

    [Fact]
    public void An_unlock_holds_the_request_again()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        app.Session.Lock();

        app.Session.Unlock();

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
    }

    [Fact]
    public void A_lock_never_changes_the_saved_setting()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();

        app.Session.Lock();
        app.Session.Unlock();
        app.Session.Lock();

        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void A_start_while_locked_holds_the_request_after_the_unlock()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Session.Locked = true;
        app.App.Start();

        app.Session.Unlock();

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
    }

    [Fact]
    public void Locks_and_unlocks_change_nothing_while_the_setting_is_off()
    {
        var app = new KeepAwakeHarness();
        app.App.Start();

        app.Session.Lock();
        app.Session.Unlock();

        Assert.Equal(new KeepAwakeState(false, false, null), app.App.State);
        Assert.Empty(app.Display.SetCalls);
    }

    [Fact]
    public void Raises_Changed_after_State_changed_by_a_lock_and_an_unlock()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        var seen = new List<KeepAwakeState>();
        app.App.Changed += (_, _) => seen.Add(app.App.State);

        app.Session.Lock();
        app.Session.Unlock();

        Assert.Equal([new KeepAwakeState(true, true, null), new KeepAwakeState(true, false, null)], seen);
    }

    [Fact]
    public void A_repeated_event_leaves_the_request_held()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();

        app.Session.Raise();
        app.Session.Raise();

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
    }

    [Fact]
    public void A_lock_state_Windows_cannot_give_after_an_event_counts_as_unlocked_with_a_Warning()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Session.Locked = true;
        app.App.Start();
        app.Session.Failure = new Win32Exception(5, "Cannot say.");

        app.Session.Raise();

        Assert.False(app.App.State.Paused);
        Assert.True(app.Display.IsSet);
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Warning));
    }

    [Fact]
    public void Logs_the_request_set_and_cleared_by_a_lock_and_an_unlock_at_Information()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        var started = Informations(app);

        app.Session.Lock();
        var locked = Informations(app);
        app.Session.Unlock();

        Assert.True(locked > started);
        Assert.True(Informations(app) > locked);
        app.Log.Events.AssertNoProblems();
    }

    [Fact]
    public void A_lock_that_cannot_clear_the_request_shows_in_State_logs_an_Error_and_keeps_the_setting_on()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        app.Display.Failure = Refusal;

        app.Session.Lock();

        Assert.True(app.App.State.IsOn);
        Assert.Contains(Refusal.Message, app.App.State.Failure);
        Assert.True(app.Log.Events.SingleAt(LogEventLevel.Error).HasException(Refusal));
        Assert.Equal("on", app.SavedSetting());
    }

    [Fact]
    public void An_unlock_after_a_refused_clear_asks_Windows_to_hold_the_request_again()
    {
        var app = new KeepAwakeHarness();
        app.StartOn();
        app.Display.Failure = Refusal;
        app.Session.Lock();
        Assert.NotNull(app.App.State.Failure);
        app.Display.Failure = null;

        app.Session.Unlock();

        Assert.Equal([true, false, true], app.Display.SetCalls);
        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
    }

    [Fact]
    public void The_next_lock_and_unlock_retry_a_refused_request()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Display.Failure = Refusal;
        app.App.Start();
        app.Display.Failure = null;

        app.Session.Lock();
        app.Session.Unlock();

        Assert.Equal(new KeepAwakeState(true, false, null), app.App.State);
        Assert.True(app.Display.IsSet);
    }
}
