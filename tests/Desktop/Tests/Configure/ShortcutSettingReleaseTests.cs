using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Releasing a shortcut so its keys reach a field, registering it again afterwards, and the message about modifier
/// keys that a taken shortcut leaves behind.
/// </summary>
[Unit]
public class ShortcutSettingReleaseTests
{
    private readonly TestShortcutSetting row = TestShortcutSetting.AddNewTask().Connected();

    [Fact]
    public void Release_unregisters_the_action()
    {
        row.Setting.Release();

        Assert.Equal([ShortcutCall.Unregister()], row.Action.Calls);
    }

    [Fact]
    public void Restore_registers_the_active_shortcut_again()
    {
        row.Setting.Release();
        row.Action.ClearCalls();

        row.Setting.Restore();

        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], row.Action.Calls);
        Assert.Equal(TestShortcut.AddNewTaskDefault, row.Setting.Active);
        Assert.Null(row.Setting.Message);
    }

    [Fact]
    public void Restore_registers_nothing_for_a_row_without_a_shortcut()
    {
        var empty = TestShortcutSetting.AddNewTask();
        empty.Action.Answer = _ => false;
        empty.Connected();

        empty.Setting.Restore();

        Assert.Empty(empty.Action.Calls);
    }

    [Fact]
    public void A_shortcut_that_cannot_register_again_leaves_the_row_empty_and_says_why()
    {
        row.Setting.Release();
        row.Action.Answer = _ => false;

        row.Setting.Restore();

        Assert.Null(row.Setting.Active);
        Assert.Equal(
            "Windows or another app already uses Win+Alt+N, so Add new task has no shortcut. Click the box and press a different one.",
            row.Setting.Message);
    }

    [Fact]
    public void A_shortcut_whose_action_throws_when_registering_again_shows_the_error()
    {
        row.Setting.Release();
        row.Action.RegisterFailure = new InvalidOperationException("Windows refused.");

        row.Setting.Restore();

        Assert.Null(row.Setting.Active);
        Assert.Equal("Windows refused.", row.Setting.Message);
    }

    [Fact]
    public void A_shortcut_that_cannot_be_released_stays_active_and_shows_the_error()
    {
        row.Action.UnregisterFailure = new InvalidOperationException("Windows refused to release it.");

        row.Setting.Release();

        Assert.Equal(TestShortcut.AddNewTaskDefault, row.Setting.Active);
        Assert.Equal("Windows refused to release it.", row.Setting.Message);
    }

    [Fact]
    public void Modifier_keys_released_with_no_other_key_say_the_shortcut_may_be_taken()
    {
        row.Setting.KeySwallowed(ModifierKeys.Control | ModifierKeys.Shift);

        Assert.Equal(
            "If you pressed a key with Ctrl+Shift, Windows or another app uses that shortcut. Press a different one.",
            row.Setting.Message);
        Assert.Empty(row.Action.Calls);
    }

    [Fact]
    public void The_taken_shortcut_message_names_the_modifiers_in_the_order_Win_Ctrl_Alt_Shift()
    {
        row.Setting.KeySwallowed(ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows);

        Assert.Equal(
            "If you pressed a key with Win+Alt+Shift, Windows or another app uses that shortcut. Press a different one.",
            row.Setting.Message);
    }

    [Fact]
    public void A_later_valid_pick_clears_the_taken_shortcut_message()
    {
        row.Setting.KeySwallowed(ModifierKeys.Control | ModifierKeys.Shift);

        row.Setting.Pick(ModifierKeys.Control | ModifierKeys.Shift, Key.K, TestShortcutSetting.ManageTasks().Setting);

        Assert.Null(row.Setting.Message);
    }
}
