using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Connecting a shortcut setting to its action: the row registers the stored shortcut, or the default when none is
/// stored or the stored text is unusable, and says whether it registered. The message wording is part of the spec.
/// </summary>
[Unit]
public class ShortcutSettingConnectTests
{
    private static readonly Shortcut CtrlAltK = TestShortcut.A;

    [Fact]
    public void Registers_the_default_once_when_nothing_is_stored()
    {
        var row = TestShortcutSetting.AddNewTask();

        var registered = row.Setting.Connect(row.Action);

        Assert.True(registered);
        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], row.Action.Calls);
        Assert.Equal(TestShortcut.AddNewTaskDefault, row.Setting.Active);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Registers_the_Task_View_default_on_its_own_action()
    {
        var row = TestShortcutSetting.ManageTasks();

        row.Setting.Connect(row.Action);

        Assert.Equal([ShortcutCall.Register(TestShortcut.ManageTasksDefault)], row.Action.Calls);
    }

    [Fact]
    public void Registers_the_stored_shortcut_instead_of_the_default()
    {
        var row = TestShortcutSetting.AddNewTask();
        row.Stored = "Ctrl+Alt+K";

        row.Setting.Connect(row.Action);

        Assert.Equal([ShortcutCall.Register(CtrlAltK)], row.Action.Calls);
        Assert.Equal(CtrlAltK, row.Setting.Active);
    }

    [Fact]
    public void Uses_the_default_and_says_why_when_the_stored_text_is_not_a_shortcut()
    {
        var row = TestShortcutSetting.AddNewTask();
        row.Stored = "not a Windows shortcut";

        var registered = row.Setting.Connect(row.Action);

        Assert.True(registered);
        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], row.Action.Calls);
        Assert.Equal(
            "The saved Add new task shortcut \"not a Windows shortcut\" isn't a valid shortcut.", row.Setting.Message);
    }

    [Fact]
    public void Uses_the_default_and_says_why_when_the_stored_text_cannot_be_read()
    {
        var row = TestShortcutSetting.AddNewTask();
        row.ReadFailure = new InvalidDataException("The settings file is damaged.");

        var registered = row.Setting.Connect(row.Action);

        Assert.True(registered);
        Assert.Equal(TestShortcut.AddNewTaskDefault, row.Setting.Active);
        Assert.Equal(
            "The saved Add new task shortcut couldn't be read: The settings file is damaged.", row.Setting.Message);
    }

    [Fact]
    public void Leaves_the_row_empty_and_says_why_when_the_default_did_not_register()
    {
        var row = TestShortcutSetting.AddNewTask();
        row.Action.Answer = _ => false;

        var registered = row.Setting.Connect(row.Action);

        Assert.False(registered);
        Assert.Null(row.Setting.Active);
        Assert.Equal(
            "Windows or another app already uses Win+Alt+N, so Add new task has no shortcut. Click the box and press a different one.",
            row.Setting.Message);
    }

    [Fact]
    public void Leaves_the_row_empty_when_the_stored_shortcut_did_not_register_and_does_not_try_the_default()
    {
        var row = TestShortcutSetting.AddNewTask();
        row.Stored = "Ctrl+Alt+K";
        row.Action.Answer = _ => false;

        var registered = row.Setting.Connect(row.Action);

        Assert.False(registered);
        Assert.Null(row.Setting.Active);
        Assert.Equal([ShortcutCall.Register(CtrlAltK)], row.Action.Calls);
    }

    [Fact]
    public void Leaves_the_row_empty_and_shows_the_error_when_the_action_throws()
    {
        var row = TestShortcutSetting.ManageTasks();
        row.Action.RegisterFailure = new InvalidOperationException("Windows refused.");

        var registered = row.Setting.Connect(row.Action);

        Assert.False(registered);
        Assert.Null(row.Setting.Active);
        Assert.Equal("Windows refused.", row.Setting.Message);
    }
}
