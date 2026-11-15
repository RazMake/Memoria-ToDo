using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// A pick on one shortcut setting: it is checked in the order the design lists (the rules, then the other row, then the
/// row's own action), and a pick that isn't used leaves the previous shortcut and says why. The message wording is
/// part of the spec.
/// </summary>
[Unit]
public class ShortcutSettingPickTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Shift = ModifierKeys.Shift;
    private const ModifierKeys Win = ModifierKeys.Windows;

    private const string OneModifier = "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus one other key.";

    private static readonly Shortcut CtrlShiftK = TestShortcut.Of(Ctrl | Shift, Key.K);

    private readonly TestShortcutSetting add = TestShortcutSetting.AddNewTask().Connected();
    private readonly TestShortcutSetting manage = TestShortcutSetting.ManageTasks().Connected();

    private void PickOnAdd(ModifierKeys modifiers, Key key) => add.Setting.Pick(modifiers, key, manage.Setting);

    private void PickOnManage(ModifierKeys modifiers, Key key) => manage.Setting.Pick(modifiers, key, add.Setting);

    // A valid pick.

    [Fact]
    public void A_valid_pick_registers_on_its_own_action_and_becomes_active()
    {
        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Equal([ShortcutCall.Register(CtrlShiftK)], add.Action.Calls);
        Assert.Equal(CtrlShiftK, add.Setting.Active);
        Assert.Null(add.Setting.Message);
    }

    [Fact]
    public void A_valid_pick_does_not_touch_the_other_row()
    {
        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Empty(manage.Action.Calls);
        Assert.Equal(TestShortcut.ManageTasksDefault, manage.Setting.Active);
        Assert.Null(manage.Setting.Message);
    }

    [Fact]
    public void A_valid_pick_is_saved_as_its_display_text()
    {
        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Equal(["Ctrl+Shift+K"], add.Saved);
    }

    [Fact]
    public void A_valid_pick_clears_the_rows_message_but_not_the_other_rows()
    {
        PickOnAdd(Ctrl, Key.C);
        PickOnManage(Ctrl, Key.C);

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Null(add.Setting.Message);
        Assert.Equal(OneModifier, manage.Setting.Message);
    }

    [Fact]
    public void A_second_pick_replaces_the_first()
    {
        PickOnAdd(Ctrl | Shift, Key.K);

        PickOnAdd(Ctrl | Shift, Key.J);

        Assert.Equal(TestShortcut.B, add.Setting.Active);
        Assert.Equal(["Ctrl+Shift+K", "Ctrl+Shift+J"], add.Saved);
    }

    [Fact]
    public void A_pick_fills_a_row_that_has_no_shortcut()
    {
        var empty = TestShortcutSetting.AddNewTask();
        empty.Action.Answer = _ => false;
        empty.Connected();
        empty.Action.Answer = _ => true;

        empty.Setting.Pick(Ctrl | Shift, Key.K, manage.Setting);

        Assert.Equal(CtrlShiftK, empty.Setting.Active);
        Assert.Null(empty.Setting.Message);
    }

    // A pick that breaks a rule.

    [Theory]
    [InlineData(Ctrl, Key.C, OneModifier)]
    [InlineData(ModifierKeys.None, Key.K, OneModifier)]
    [InlineData(Ctrl | Shift, Key.F12, "F12 is kept by Windows for debuggers.")]
    [InlineData(Ctrl, Key.F12, "F12 is kept by Windows for debuggers.")]
    [InlineData(Ctrl | Alt, Key.LeftShift, "Add a key besides the modifier keys.")]
    public void A_pick_that_breaks_a_rule_shows_which_one_and_changes_nothing(
        ModifierKeys modifiers, Key key, string message)
    {
        PickOnAdd(modifiers, key);

        Assert.Equal(message, add.Setting.Message);
        Assert.Empty(add.Action.Calls);
        Assert.Empty(add.Saved);
        Assert.Equal(TestShortcut.AddNewTaskDefault, add.Setting.Active);
        Assert.Null(manage.Setting.Message);
    }

    // A pick the other row uses, or the row's own shortcut.

    [Fact]
    public void A_pick_equal_to_the_Task_View_shortcut_is_refused_without_calling_an_action()
    {
        PickOnAdd(Win | Alt, Key.V);

        Assert.Equal("Task View already uses Win+Alt+V. Press a different shortcut.", add.Setting.Message);
        Assert.Empty(add.Action.Calls);
        Assert.Empty(add.Saved);
        Assert.Equal(TestShortcut.AddNewTaskDefault, add.Setting.Active);
    }

    [Fact]
    public void A_pick_equal_to_the_Add_new_task_shortcut_is_refused_without_calling_an_action()
    {
        PickOnManage(Win | Alt, Key.N);

        Assert.Equal("Add new task already uses Win+Alt+N. Press a different shortcut.", manage.Setting.Message);
        Assert.Empty(manage.Action.Calls);
    }

    [Fact]
    public void A_pick_equal_to_the_other_rows_changed_shortcut_is_refused()
    {
        PickOnManage(Ctrl | Shift, Key.K);

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Equal("Task View already uses Ctrl+Shift+K. Press a different shortcut.", add.Setting.Message);
        Assert.Empty(add.Action.Calls);
    }

    [Fact]
    public void A_pick_equal_to_the_rows_own_shortcut_calls_nothing_and_saves_nothing()
    {
        PickOnAdd(Win | Alt, Key.N);

        Assert.Empty(add.Action.Calls);
        Assert.Empty(add.Saved);
        Assert.Equal(TestShortcut.AddNewTaskDefault, add.Setting.Active);
        Assert.Null(add.Setting.Message);
    }

    [Fact]
    public void A_pick_equal_to_the_rows_own_shortcut_leaves_its_message_as_it_was()
    {
        PickOnAdd(Ctrl, Key.C);

        PickOnAdd(Win | Alt, Key.N);

        Assert.Equal(OneModifier, add.Setting.Message);
    }

    // A pick Windows or another app holds, or an action that fails.

    [Fact]
    public void A_pick_that_Windows_holds_says_so_and_keeps_the_previous_shortcut()
    {
        manage.Action.Answer = shortcut => shortcut == TestShortcut.ManageTasksDefault;

        PickOnManage(Ctrl | Shift, Key.K);

        Assert.Equal(
            "Windows or another app already uses Ctrl+Shift+K. Press a different shortcut.", manage.Setting.Message);
        Assert.Equal(TestShortcut.ManageTasksDefault, manage.Setting.Active);
        Assert.Empty(manage.Saved);
        Assert.Null(add.Setting.Message);
    }

    [Fact]
    public void An_action_that_throws_shows_the_error_and_keeps_the_previous_shortcut()
    {
        add.Action.RegisterFailure = new InvalidOperationException("Windows refused.");

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Equal("Windows refused.", add.Setting.Message);
        Assert.Equal(TestShortcut.AddNewTaskDefault, add.Setting.Active);
        Assert.Empty(add.Saved);
    }

    // A pick that registers but can't be saved.

    [Fact]
    public void A_pick_that_cannot_be_saved_goes_back_to_the_previous_shortcut_and_says_what_failed()
    {
        add.SaveFailure = new IOException("The disk is full.");

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Equal(TestShortcut.AddNewTaskDefault, add.Setting.Active);
        Assert.Equal(
            [ShortcutCall.Register(CtrlShiftK), ShortcutCall.Register(TestShortcut.AddNewTaskDefault)],
            add.Action.Calls);
        Assert.Equal("Ctrl+Shift+K wasn't saved for Add new task: The disk is full.", add.Setting.Message);
    }

    [Fact]
    public void A_pick_that_cannot_be_saved_says_so_when_the_previous_shortcut_cannot_register_again()
    {
        add.SaveFailure = new IOException("The disk is full.");
        add.Action.Answer = shortcut => shortcut == CtrlShiftK;

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Null(add.Setting.Active);
        Assert.Equal(
            "Ctrl+Shift+K wasn't saved for Add new task: The disk is full. "
            + "Win+Alt+N couldn't be registered again, so Add new task has no shortcut.",
            add.Setting.Message);
    }

    [Fact]
    public void A_pick_that_cannot_be_saved_says_so_when_registering_the_previous_shortcut_throws()
    {
        add.SaveFailure = new IOException("The disk is full.");
        add.Action.Answer = _ =>
        {
            add.Action.RegisterFailure = new InvalidOperationException("Windows refused.");
            return true;
        };

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Null(add.Setting.Active);
        Assert.Equal(
            "Ctrl+Shift+K wasn't saved for Add new task: The disk is full. "
            + "Add new task's previous shortcut couldn't be restored: Windows refused.",
            add.Setting.Message);
    }

    [Fact]
    public void A_pick_that_cannot_be_saved_unregisters_the_action_when_the_row_had_no_shortcut()
    {
        var empty = TestShortcutSetting.AddNewTask();
        empty.Action.Answer = _ => false;
        empty.Connected();
        empty.Action.Answer = _ => true;
        empty.SaveFailure = new IOException("The disk is full.");

        empty.Setting.Pick(Ctrl | Shift, Key.K, manage.Setting);

        Assert.Null(empty.Setting.Active);
        Assert.Equal([ShortcutCall.Register(CtrlShiftK), ShortcutCall.Unregister()], empty.Action.Calls);
    }

    // A pick while a shortcut field has released the shortcut.

    [Fact]
    public void A_pick_while_released_registers_to_check_it_and_unregisters_again()
    {
        add.Setting.Release();

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Equal(
            [ShortcutCall.Unregister(), ShortcutCall.Register(CtrlShiftK), ShortcutCall.Unregister()],
            add.Action.Calls);
        Assert.Equal(CtrlShiftK, add.Setting.Active);
        Assert.Equal(["Ctrl+Shift+K"], add.Saved);
    }

    [Fact]
    public void A_pick_that_cannot_be_saved_while_released_does_not_register_the_previous_shortcut()
    {
        add.Setting.Release();
        add.SaveFailure = new IOException("The disk is full.");

        PickOnAdd(Ctrl | Shift, Key.K);

        Assert.Equal(TestShortcut.AddNewTaskDefault, add.Setting.Active);
        Assert.Equal(ShortcutCall.Unregister(), add.Action.Calls[^1]);
        Assert.DoesNotContain(add.Action.Calls, call => call == ShortcutCall.Register(TestShortcut.AddNewTaskDefault));
    }
}
