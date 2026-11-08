using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// While a shortcut box has the focus, both shortcuts are released so their keys reach the box; when the focus
/// leaves the boxes, each row's shortcut is registered again. "Another element" is the Start with Windows check box.
/// </summary>
[UX]
public class ConfigureShortcutFocusTests
{
    private static readonly Shortcut CtrlShiftK = TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Shift, Key.K);

    private static ShortcutCall Last(FakeShortcutAction action) => action.Calls[^1];

    private static ShortcutCall[] Registrations(FakeShortcutAction action) =>
        [.. action.Calls.Where(call => !call.IsUnregister)];

    [WpfFact]
    public void Opening_the_window_registers_and_unregisters_nothing()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Empty(screen.ManageTasks.Calls);
    }

    [WpfFact]
    public void Opens_with_no_shortcut_box_focused()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.False(screen.AddField.IsKeyboardFocusWithin);
        Assert.False(screen.ManageField.IsKeyboardFocusWithin);
    }

    [WpfFact]
    public void A_box_getting_focus_unregisters_both_actions()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        ConfigureScreen.Focus(screen.AddField);

        Assert.NotEmpty(screen.AddNewTask.Calls);
        Assert.NotEmpty(screen.ManageTasks.Calls);
        Assert.All(screen.AddNewTask.Calls, call => Assert.True(call.IsUnregister));
        Assert.All(screen.ManageTasks.Calls, call => Assert.True(call.IsUnregister));
    }

    [WpfFact]
    public void Moving_the_focus_between_the_boxes_registers_nothing_in_between()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);

        ConfigureScreen.Focus(screen.ManageField);

        Assert.Empty(Registrations(screen.AddNewTask));
        Assert.Empty(Registrations(screen.ManageTasks));
    }

    [WpfFact]
    public void The_focus_leaving_the_boxes_registers_each_rows_shortcut_again()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);
        ConfigureScreen.Focus(screen.ManageField);

        screen.FocusStartWithWindowsBox();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), Last(screen.AddNewTask));
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), Last(screen.ManageTasks));
        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], Registrations(screen.AddNewTask));
        Assert.Equal([ShortcutCall.Register(TestShortcut.ManageTasksDefault)], Registrations(screen.ManageTasks));
    }

    [WpfFact]
    public void Closing_the_window_while_a_box_has_the_focus_registers_both_shortcuts_again()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.ManageField);

        screen.CloseWindow();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), Last(screen.AddNewTask));
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), Last(screen.ManageTasks));
    }

    [WpfFact]
    public void A_pick_while_focused_registers_then_unregisters_and_the_focus_leaving_registers_it_with_the_other_row()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal([ShortcutCall.Register(CtrlShiftK)], Registrations(screen.AddNewTask));
        Assert.True(Last(screen.AddNewTask).IsUnregister);
        Assert.Equal("Ctrl+Shift+K", screen.AddField.Text);

        screen.FocusStartWithWindowsBox();

        Assert.Equal(ShortcutCall.Register(CtrlShiftK), Last(screen.AddNewTask));
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), Last(screen.ManageTasks));
    }

    [WpfFact]
    public void A_shortcut_that_cannot_register_again_leaves_its_row_empty_and_says_why()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);
        screen.AddNewTask.Answer = _ => false;

        screen.FocusStartWithWindowsBox();

        Assert.Equal("", screen.AddField.Text);
        Assert.Equal(
            "Windows or another app already uses Win+Alt+N, so Add new task has no shortcut. Click the box and press a different one.",
            screen.AddMessage);
        Assert.Equal("Win+Alt+Shift+T", screen.ManageField.Text);
        Assert.Equal("", screen.ManageMessage);
    }

    [WpfFact]
    public void A_shortcut_whose_action_throws_when_registering_again_shows_the_error()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.ManageField);
        screen.ManageTasks.RegisterFailure = new InvalidOperationException("Windows refused.");

        screen.FocusStartWithWindowsBox();

        Assert.Equal("Windows refused.", screen.ManageMessage);
        Assert.Equal("", screen.AddMessage);
    }

    [WpfFact]
    public void A_shortcut_that_cannot_be_released_when_a_box_gets_focus_shows_the_error_and_the_other_is_still_released()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.ManageTasks.UnregisterFailure = new InvalidOperationException("Windows refused to release it.");

        ConfigureScreen.Focus(screen.AddField);

        Assert.Equal("Windows refused to release it.", screen.ManageMessage);
        Assert.Equal("Win+Alt+Shift+T", screen.ManageField.Text);
        Assert.Equal(ShortcutCall.Unregister(), Last(screen.AddNewTask));
        Assert.Equal("", screen.AddMessage);
    }
}
