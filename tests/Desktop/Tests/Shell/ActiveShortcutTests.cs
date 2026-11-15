using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>The rules for one shortcut action (Add new task or Task View), checked on their own against the fake port.</summary>
[Unit]
public class ActiveShortcutTests
{
    private readonly FakeGlobalShortcut port = new();
    private int pressed;
    private readonly ActiveShortcut action;

    public ActiveShortcutTests() => action = new ActiveShortcut(port, () => pressed++);

    [Fact]
    public void Registering_a_new_shortcut_returns_true_and_pressing_it_runs_the_action()
    {
        var registered = action.Register(TestShortcut.A);
        port.Press(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal([TestShortcut.A], port.Registered);
        Assert.Equal(1, pressed);
    }

    [Fact]
    public void A_taken_shortcut_returns_false()
    {
        port.HoldByAnotherApp(TestShortcut.B);

        Assert.False(action.Register(TestShortcut.B));
    }

    [Fact]
    public void A_taken_shortcut_leaves_the_previous_one_registered_and_pressable()
    {
        action.Register(TestShortcut.A);
        port.HoldByAnotherApp(TestShortcut.B);

        action.Register(TestShortcut.B);
        port.Press(TestShortcut.A);

        Assert.Equal([TestShortcut.A], port.Registered);
        Assert.Equal(1, pressed);
    }

    [Fact]
    public void A_new_shortcut_that_registers_releases_the_previous_one()
    {
        action.Register(TestShortcut.A);

        action.Register(TestShortcut.B);
        port.Press(TestShortcut.B);

        Assert.Equal([TestShortcut.B], port.Registered);
        Assert.Equal(1, pressed);
    }

    [Fact]
    public void Registering_the_active_shortcut_again_returns_true_without_calling_the_port()
    {
        action.Register(TestShortcut.A);
        var registrations = port.RegisterCount;

        var registered = action.Register(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal(registrations, port.RegisterCount);
        Assert.Equal([TestShortcut.A], port.Registered);
    }

    [Fact]
    public void A_port_failure_other_than_a_taken_shortcut_passes_on_to_the_caller()
    {
        port.RegisterFailure = new InvalidOperationException("Windows refused.");

        var error = Assert.Throws<InvalidOperationException>(() => action.Register(TestShortcut.A));

        Assert.Equal("Windows refused.", error.Message);
    }

    [Fact]
    public void A_failed_release_of_the_previous_shortcut_passes_on_and_leaves_it_registered_and_pressable()
    {
        action.Register(TestShortcut.A);
        port.FailUnregister(TestShortcut.A, new InvalidOperationException("Windows won't release."));

        var error = Assert.Throws<InvalidOperationException>(() => action.Register(TestShortcut.B));
        port.Press(TestShortcut.A);

        Assert.Equal("Windows won't release.", error.Message);
        Assert.Equal([TestShortcut.A], port.Registered);
        Assert.Equal(1, pressed);
    }

    [Fact]
    public void A_failed_release_whose_undo_also_fails_passes_on_both_failures_and_keeps_the_previous_shortcut_active()
    {
        action.Register(TestShortcut.A);
        var releaseFailure = new InvalidOperationException("Windows won't release A.");
        var undoFailure = new InvalidOperationException("Windows won't release B.");
        port.FailUnregister(TestShortcut.A, releaseFailure);
        port.FailUnregister(TestShortcut.B, undoFailure);

        var error = Assert.Throws<AggregateException>(() => action.Register(TestShortcut.B));
        var registrations = port.RegisterCount;
        var registeredAgain = action.Register(TestShortcut.A);

        Assert.Equal([releaseFailure, undoFailure], error.InnerExceptions);
        Assert.True(registeredAgain);
        Assert.Equal(registrations, port.RegisterCount);
    }

    [Fact]
    public void Unregister_releases_the_shortcut_so_pressing_it_does_nothing()
    {
        action.Register(TestShortcut.A);

        action.Unregister();

        Assert.Empty(port.Registered);
        Assert.Throws<InvalidOperationException>(() => port.Press(TestShortcut.A));
        Assert.Equal(0, pressed);
    }

    [Fact]
    public void Unregister_does_nothing_when_the_action_has_no_shortcut()
    {
        action.Unregister();

        Assert.Empty(port.Registered);
    }

    [Fact]
    public void Registering_a_missing_shortcut_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => action.Register(null!));
        Assert.Empty(port.Registered);
    }

    [Fact]
    public void A_failed_Unregister_passes_on_the_error_and_leaves_the_shortcut_pressable()
    {
        action.Register(TestShortcut.A);
        port.FailUnregister(TestShortcut.A, new InvalidOperationException("Windows won't release."));

        var error = Assert.Throws<InvalidOperationException>(action.Unregister);
        port.Press(TestShortcut.A);

        Assert.Equal("Windows won't release.", error.Message);
        Assert.Equal([TestShortcut.A], port.Registered);
        Assert.Equal(1, pressed);
    }

    [Fact]
    public void Registering_after_Unregister_registers_the_same_shortcut_again()
    {
        action.Register(TestShortcut.A);
        action.Unregister();

        var registered = action.Register(TestShortcut.A);
        port.Press(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal([TestShortcut.A], port.Registered);
        Assert.Equal(1, pressed);
    }
}
