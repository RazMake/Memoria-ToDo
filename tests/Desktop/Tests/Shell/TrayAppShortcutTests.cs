using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// How Shell wires the two shortcut actions Configure gets (Add new task and Task View) to the windows. The rules
/// of one action are in <c>ActiveShortcutTests</c>.
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
    public void A_press_while_Configure_is_open_shows_and_closes_no_window(ShortcutKind kind)
    {
        var app = Started();
        app.ActionOf(kind).Register(TestShortcut.A);
        app.Configure.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Empty(app.Shown);
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
}
