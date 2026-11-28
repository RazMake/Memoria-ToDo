using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// While a shortcut box has the keyboard focus, both shortcuts are released so their keys reach the box; when the
/// focus leaves the boxes, each row's shortcut is registered again. Moving from one box to the other keeps them released.
/// </summary>
[Unit]
public class ShortcutFocusTests
{
    private readonly TestShortcutSetting _add = TestShortcutSetting.AddNewTask().Connected();
    private readonly TestShortcutSetting _manage = TestShortcutSetting.ManageTasks().Connected();
    private int _changed;
    private readonly ShortcutFocus _focus;

    public ShortcutFocusTests()
    {
        _focus = new ShortcutFocus([_add.Setting, _manage.Setting], () => _changed++);
    }

    [Fact]
    public void Creating_it_registers_and_unregisters_nothing()
    {
        Assert.Empty(_add.Action.Calls);
        Assert.Empty(_manage.Action.Calls);
        Assert.Equal(0, _changed);
    }

    [Fact]
    public void A_box_getting_focus_unregisters_both_actions_and_tells_the_window()
    {
        _focus.FieldFocused();

        Assert.Equal([ShortcutCall.Unregister()], _add.Action.Calls);
        Assert.Equal([ShortcutCall.Unregister()], _manage.Action.Calls);
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void Moving_the_focus_between_the_boxes_releases_and_registers_nothing_more()
    {
        _focus.FieldFocused();

        _focus.FocusLeft(toAnotherField: true);
        _focus.FieldFocused();

        Assert.Equal([ShortcutCall.Unregister()], _add.Action.Calls);
        Assert.Equal([ShortcutCall.Unregister()], _manage.Action.Calls);
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void The_focus_leaving_the_boxes_registers_each_rows_shortcut_again_and_tells_the_window()
    {
        _focus.FieldFocused();
        _add.Action.ClearCalls();
        _manage.Action.ClearCalls();

        _focus.FocusLeft(toAnotherField: false);

        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], _add.Action.Calls);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ManageTasksDefault)], _manage.Action.Calls);
        Assert.Equal(2, _changed);
    }

    [Fact]
    public void The_focus_leaving_when_nothing_is_released_does_nothing()
    {
        _focus.FocusLeft(toAnotherField: false);

        Assert.Empty(_add.Action.Calls);
        Assert.Equal(0, _changed);
    }

    [Fact]
    public void Restore_registers_both_shortcuts_again_when_a_box_still_holds_them_released()
    {
        _focus.FieldFocused();

        _focus.Restore();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), _add.Action.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), _manage.Action.Calls[^1]);
    }

    [Fact]
    public void Restore_twice_registers_only_once()
    {
        _focus.FieldFocused();
        _focus.Restore();
        _add.Action.ClearCalls();

        _focus.Restore();

        Assert.Empty(_add.Action.Calls);
    }

    [Fact]
    public void A_box_can_get_focus_again_after_the_shortcuts_were_restored()
    {
        _focus.FieldFocused();
        _focus.Restore();
        _add.Action.ClearCalls();

        _focus.FieldFocused();

        Assert.Equal([ShortcutCall.Unregister()], _add.Action.Calls);
    }

    [Fact]
    public void A_pick_while_focused_is_registered_then_released_and_the_focus_leaving_registers_it()
    {
        _focus.FieldFocused();
        _add.Action.ClearCalls();

        _add.Setting.Pick(ModifierKeys.Control | ModifierKeys.Shift, Key.K, _manage.Setting);
        var picked = TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Shift, Key.K);

        Assert.Equal([ShortcutCall.Register(picked), ShortcutCall.Unregister()], _add.Action.Calls);
        _focus.FocusLeft(toAnotherField: false);
        Assert.Equal(ShortcutCall.Register(picked), _add.Action.Calls[^1]);
    }

    [Fact]
    public void A_shortcut_that_cannot_register_again_leaves_its_row_empty_and_says_why()
    {
        _focus.FieldFocused();
        _add.Action.Answer = _ => false;

        _focus.FocusLeft(toAnotherField: false);

        Assert.Null(_add.Setting.Active);
        Assert.Contains("Win+Alt+N", _add.Setting.Message);
        Assert.Equal(TestShortcut.ManageTasksDefault, _manage.Setting.Active);
        Assert.Null(_manage.Setting.Message);
    }

    [Fact]
    public void An_action_that_cannot_be_released_shows_the_error_and_the_other_is_still_released()
    {
        _manage.Action.UnregisterFailure = new InvalidOperationException("Windows refused to release it.");

        _focus.FieldFocused();

        Assert.Equal("Windows refused to release it.", _manage.Setting.Message);
        Assert.Equal([ShortcutCall.Unregister()], _add.Action.Calls);
        Assert.Null(_add.Setting.Message);
    }
}
