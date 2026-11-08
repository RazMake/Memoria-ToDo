using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// The two shortcut actions Shell gives Configure, one for Add new task and one for Manage tasks. Each follows the
/// same rules, so most scenarios run once for each action.
/// </summary>
[Unit]
public class TrayAppShortcutTests
{
    private static ShellHarness Started()
    {
        var app = new ShellHarness();
        app.Start();
        return app;
    }

    // Which shortcut opens which window.

    [Fact]
    public void Pressing_the_Add_new_task_shortcut_shows_only_Add()
    {
        var app = Started();
        app.Configure.AddNewTask.Register(TestShortcut.A);

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(1, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
    }

    [Fact]
    public void Pressing_the_Manage_tasks_shortcut_shows_only_View_Tasks()
    {
        var app = Started();
        app.Configure.ManageTasks.Register(TestShortcut.B);

        app.GlobalShortcut.Press(TestShortcut.B);

        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(1, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
    }

    // The two actions don't affect each other.

    [Fact]
    public void Replacing_one_actions_shortcut_leaves_the_other_actions_shortcut_registered()
    {
        var app = Started();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);

        app.Configure.AddNewTask.Register(TestShortcut.AddNewTaskDefault);

        Assert.Equivalent(new[] { TestShortcut.AddNewTaskDefault, TestShortcut.B }, app.GlobalShortcut.Registered);
    }

    [Fact]
    public void Replacing_one_actions_shortcut_keeps_both_opening_their_own_windows()
    {
        var app = Started();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);
        app.Configure.AddNewTask.Register(TestShortcut.AddNewTaskDefault);

        app.GlobalShortcut.Press(TestShortcut.B);
        app.GlobalShortcut.Press(TestShortcut.AddNewTaskDefault);

        Assert.Equal(1, app.ViewTasks.ShowCount);
        Assert.Equal(1, app.Add.ShowCount);
    }

    [Fact]
    public void Releasing_one_action_leaves_the_other_actions_shortcut_registered()
    {
        var app = Started();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ManageTasks.Register(TestShortcut.B);

        app.Configure.AddNewTask.Unregister();

        Assert.Equal([TestShortcut.B], app.GlobalShortcut.Registered);
    }

    [Fact]
    public void A_taken_shortcut_on_one_action_leaves_the_other_actions_shortcut_registered()
    {
        var app = Started();
        app.Configure.ManageTasks.Register(TestShortcut.B);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);

        var taken = app.Configure.AddNewTask.Register(TestShortcut.A);

        Assert.False(taken);
        Assert.Equal([TestShortcut.B], app.GlobalShortcut.Registered);
    }

    // Register, for each action.

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void Registering_a_new_shortcut_returns_true_and_registers_it(ShortcutKind kind)
    {
        var app = Started();

        var registered = app.ActionOf(kind).Register(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_registered_shortcut_shows_only_its_own_window_when_pressed(ShortcutKind kind)
    {
        var app = Started();
        app.ActionOf(kind).Register(TestShortcut.A);

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(1, app.ShownBy(kind));
        Assert.Equal(0, app.ShownByOtherThan(kind));
        Assert.Equal(0, app.Configure.ShowCount);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_taken_shortcut_returns_false(ShortcutKind kind)
    {
        var app = Started();
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.B);

        var registered = app.ActionOf(kind).Register(TestShortcut.B);

        Assert.False(registered);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_taken_shortcut_leaves_the_previous_one_registered_and_pressable(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.B);

        action.Register(TestShortcut.B);
        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
        Assert.Equal(1, app.ShownBy(kind));
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_new_shortcut_that_registers_releases_the_previous_one_and_is_pressable(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);

        action.Register(TestShortcut.B);
        app.GlobalShortcut.Press(TestShortcut.B);

        Assert.Equal([TestShortcut.B], app.GlobalShortcut.Registered);
        Assert.Equal(1, app.ShownBy(kind));
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void Registering_the_actions_own_shortcut_again_returns_true_without_calling_the_port(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);
        var registrations = app.GlobalShortcut.RegisterCount;

        var registered = action.Register(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal(registrations, app.GlobalShortcut.RegisterCount);
        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_port_failure_other_than_a_taken_shortcut_passes_on_to_the_caller(ShortcutKind kind)
    {
        var app = Started();
        app.GlobalShortcut.RegisterFailure = new InvalidOperationException("Windows refused.");

        var error = Assert.Throws<InvalidOperationException>(() => app.ActionOf(kind).Register(TestShortcut.A));

        Assert.Equal("Windows refused.", error.Message);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_failed_release_of_the_previous_shortcut_passes_on_and_leaves_it_registered_and_pressable(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);
        app.GlobalShortcut.FailUnregister(TestShortcut.A, new InvalidOperationException("Windows won't release."));

        var error = Assert.Throws<InvalidOperationException>(() => action.Register(TestShortcut.B));
        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal("Windows won't release.", error.Message);
        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
        Assert.Equal(1, app.ShownBy(kind));
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_failed_release_whose_undo_also_fails_passes_on_both_failures(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);
        var releaseFailure = new InvalidOperationException("Windows won't release A.");
        var undoFailure = new InvalidOperationException("Windows won't release B.");
        app.GlobalShortcut.FailUnregister(TestShortcut.A, releaseFailure);
        app.GlobalShortcut.FailUnregister(TestShortcut.B, undoFailure);

        var error = Assert.Throws<AggregateException>(() => action.Register(TestShortcut.B));

        Assert.Equal([releaseFailure, undoFailure], error.InnerExceptions);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_failed_release_whose_undo_also_fails_keeps_the_previous_shortcut_as_the_actions(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);
        app.GlobalShortcut.FailUnregister(TestShortcut.A, new InvalidOperationException("Windows won't release A."));
        app.GlobalShortcut.FailUnregister(TestShortcut.B, new InvalidOperationException("Windows won't release B."));
        Assert.ThrowsAny<Exception>(() => action.Register(TestShortcut.B));
        var registrations = app.GlobalShortcut.RegisterCount;

        var registered = action.Register(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal(registrations, app.GlobalShortcut.RegisterCount);
    }

    // Unregister, for each action.

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void Unregister_releases_the_shortcut_so_pressing_it_does_nothing(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);

        action.Unregister();

        Assert.Empty(app.GlobalShortcut.Registered);
        Assert.Equal(0, app.ShownBy(kind));
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void Unregister_does_nothing_when_the_action_has_no_shortcut(ShortcutKind kind)
    {
        var app = Started();

        app.ActionOf(kind).Unregister();

        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void Registering_a_missing_shortcut_is_rejected_when_the_action_has_none(ShortcutKind kind)
    {
        var app = Started();

        Assert.Throws<ArgumentNullException>(() => app.ActionOf(kind).Register(null!));
        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void A_failed_Unregister_passes_on_the_error_and_leaves_the_shortcut_pressable(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);
        app.GlobalShortcut.FailUnregister(TestShortcut.A, new InvalidOperationException("Windows won't release."));

        var error = Assert.Throws<InvalidOperationException>(action.Unregister);
        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal("Windows won't release.", error.Message);
        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
        Assert.Equal(1, app.ShownBy(kind));
    }

    [Theory]
    [InlineData(ShortcutKind.AddNewTask)]
    [InlineData(ShortcutKind.ManageTasks)]
    public void Registering_after_Unregister_registers_the_same_shortcut_again(ShortcutKind kind)
    {
        var app = Started();
        var action = app.ActionOf(kind);
        action.Register(TestShortcut.A);
        action.Unregister();

        var registered = action.Register(TestShortcut.A);
        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
        Assert.Equal(1, app.ShownBy(kind));
    }
}
