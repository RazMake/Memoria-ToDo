using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The two shortcut rows: the Add new task row first, the Manage tasks row second, each with its box and its message.
/// The message wording is part of the spec, so the tests state it literally. A pick is checked in the order the
/// design lists: the rules, then the other row, then the row's own action.
/// </summary>
[UX]
public class ConfigureShortcutFieldTests
{
    private const string AddDefault = "Win+Alt+N";
    private const string ManageDefault = "Win+Alt+V";
    private const string OneModifierMessage = "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus one other key.";

    [WpfFact]
    public void Show_the_default_of_each_row_in_its_box_with_no_message()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(AddDefault, screen.AddField.Text);
        Assert.Equal(ManageDefault, screen.ManageField.Text);
        Assert.Equal("", screen.AddMessage);
        Assert.Equal("", screen.ManageMessage);
    }

    // A default that didn't register.

    [WpfFact]
    public void An_Add_new_task_default_that_did_not_register_leaves_its_box_empty_and_says_why()
    {
        using var screen = new ConfigureScreen(addNewTask: new FakeShortcutAction { Answer = _ => false });

        screen.Open();

        Assert.Equal("", screen.AddField.Text);
        Assert.Equal(
            "Windows or another app already uses Win+Alt+N, so Add new task has no shortcut. Click the box and press a different one.",
            screen.AddMessage);
        Assert.False(screen.ShowsText("No shortcut is active"));
    }

    [WpfFact]
    public void A_Manage_tasks_default_that_did_not_register_leaves_its_box_empty_and_says_why()
    {
        using var screen = new ConfigureScreen(manageTasks: new FakeShortcutAction { Answer = _ => false });

        screen.Open();

        Assert.Equal("", screen.ManageField.Text);
        Assert.Equal(
            "Windows or another app already uses Win+Alt+V, so Manage tasks has no shortcut. Click the box and press a different one.",
            screen.ManageMessage);
    }

    [WpfFact]
    public void A_default_that_did_not_register_does_not_change_the_other_row()
    {
        using var screen = new ConfigureScreen(addNewTask: new FakeShortcutAction { Answer = _ => false });

        screen.Open();

        Assert.Equal(ManageDefault, screen.ManageField.Text);
        Assert.Equal("", screen.ManageMessage);
    }

    [WpfFact]
    public void A_default_whose_action_threw_leaves_its_box_empty_and_shows_the_error()
    {
        using var screen = new ConfigureScreen(
            manageTasks: new FakeShortcutAction { RegisterFailure = new InvalidOperationException("Windows refused.") });

        screen.Open();

        Assert.Equal("", screen.ManageField.Text);
        Assert.Equal("Windows refused.", screen.ManageMessage);
        Assert.Equal(AddDefault, screen.AddField.Text);
    }

    // A valid pick.

    [WpfFact]
    public void A_valid_pick_registers_on_its_own_rows_action_only()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal(
            [ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Shift, Key.K))],
            screen.ManageTasks.Calls);
        Assert.Empty(screen.AddNewTask.Calls);
    }

    [WpfFact]
    public void An_accepted_pick_shows_in_its_rows_box_and_leaves_the_other_box()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("Ctrl+Shift+K", screen.AddField.Text);
        Assert.Equal(ManageDefault, screen.ManageField.Text);
    }

    [WpfFact]
    public void A_pick_that_registers_clears_its_rows_message_and_not_the_other_rows()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, Key.LeftCtrl, Key.C);
        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.C);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("", screen.AddMessage);
        Assert.Equal(OneModifierMessage, screen.ManageMessage);
    }

    [WpfFact]
    public void Takes_a_second_pick_after_the_first()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.J);

        Assert.Equal(2, screen.AddNewTask.Calls.Count);
        Assert.Equal("Ctrl+Shift+J", screen.AddField.Text);
    }

    [WpfFact]
    public void A_pick_that_registers_fills_an_empty_box()
    {
        using var screen = new ConfigureScreen(addNewTask: new FakeShortcutAction { Answer = _ => false });
        screen.Open();
        screen.AddNewTask.Answer = _ => true;

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("Ctrl+Shift+K", screen.AddField.Text);
        Assert.Equal("", screen.AddMessage);
    }

    // The other row's shortcut.

    [WpfFact]
    public void A_pick_equal_to_the_Manage_tasks_shortcut_is_refused_without_calling_an_action()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LWin, Key.LeftAlt, Key.V);

        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Equal("Manage tasks already uses Win+Alt+V. Press a different shortcut.", screen.AddMessage);
        Assert.Equal(AddDefault, screen.AddField.Text);
    }

    [WpfFact]
    public void A_pick_equal_to_the_Add_new_task_shortcut_is_refused_without_calling_an_action()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ManageField, Key.LWin, Key.LeftAlt, Key.N);

        Assert.Empty(screen.ManageTasks.Calls);
        Assert.Equal("Add new task already uses Win+Alt+N. Press a different shortcut.", screen.ManageMessage);
        Assert.Equal(ManageDefault, screen.ManageField.Text);
    }

    [WpfFact]
    public void A_pick_equal_to_the_other_rows_changed_shortcut_is_refused()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.LeftShift, Key.K);
        screen.AddNewTask.ClearCalls();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Equal("Manage tasks already uses Ctrl+Shift+K. Press a different shortcut.", screen.AddMessage);
    }

    // The row's own shortcut.

    [WpfFact]
    public void A_pick_equal_to_the_rows_own_shortcut_calls_nothing_and_changes_nothing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LWin, Key.LeftAlt, Key.N);

        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Equal(AddDefault, screen.AddField.Text);
        Assert.Equal("", screen.AddMessage);
    }

    [WpfFact]
    public void A_pick_equal_to_the_rows_own_shortcut_leaves_a_message_as_it_was()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, Key.LeftCtrl, Key.C);

        screen.Chord(screen.AddField, Key.LWin, Key.LeftAlt, Key.N);

        Assert.Equal(OneModifierMessage, screen.AddMessage);
    }

    // A pick Windows or another app holds, or an action that fails.

    [WpfFact]
    public void A_taken_pick_says_so_and_the_box_keeps_the_previous_shortcut()
    {
        using var screen = new ConfigureScreen(
            manageTasks: new FakeShortcutAction { Answer = shortcut => shortcut == TestShortcut.ManageTasksDefault });
        screen.Open();

        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal(
            "Windows or another app already uses Ctrl+Shift+K. Press a different shortcut.",
            screen.ManageMessage);
        Assert.Equal(ManageDefault, screen.ManageField.Text);
        Assert.Equal("", screen.AddMessage);
    }

    [WpfFact]
    public void An_action_that_throws_shows_the_error_and_keeps_the_previous_shortcut()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.AddNewTask.RegisterFailure = new InvalidOperationException("Windows refused.");

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("Windows refused.", screen.AddMessage);
        Assert.Equal(AddDefault, screen.AddField.Text);
    }

    // Esc.

    [WpfFact]
    public void Esc_leaves_the_shortcut_as_it_was_and_calls_nothing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.KeyDown(screen.AddField, Key.LeftCtrl);
        screen.KeyDown(screen.AddField, Key.LeftShift);

        screen.KeyDown(screen.AddField, Key.Escape);
        screen.KeyUp(screen.AddField, Key.Escape);
        screen.KeyUp(screen.AddField, Key.LeftShift);
        screen.KeyUp(screen.AddField, Key.LeftCtrl);

        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Equal(AddDefault, screen.AddField.Text);
    }

    [WpfFact]
    public void Esc_leaves_a_message_showing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.F12);

        screen.Chord(screen.AddField, Key.Escape);

        Assert.Equal("F12 is kept by Windows for debuggers.", screen.AddMessage);
    }

    // The rules' own messages.

    [WpfFact]
    public void A_pick_with_one_modifier_shows_why_under_its_box_and_calls_nothing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.C);

        Assert.Equal(OneModifierMessage, screen.ManageMessage);
        Assert.Equal("", screen.AddMessage);
        Assert.Empty(screen.ManageTasks.Calls);
        Assert.Equal(ManageDefault, screen.ManageField.Text);
    }

    [WpfFact]
    public void A_pick_with_no_modifier_shows_why_and_calls_nothing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.K);

        Assert.Equal(OneModifierMessage, screen.AddMessage);
        Assert.Empty(screen.AddNewTask.Calls);
    }

    [WpfFact]
    public void A_pick_of_F12_shows_why_and_calls_nothing()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.F12);

        Assert.Equal("F12 is kept by Windows for debuggers.", screen.AddMessage);
        Assert.Empty(screen.AddNewTask.Calls);
        Assert.Equal(AddDefault, screen.AddField.Text);
    }

    [WpfFact]
    public void A_pick_that_breaks_both_rules_gets_the_F12_message()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.F12);

        Assert.Equal("F12 is kept by Windows for debuggers.", screen.AddMessage);
    }

    [WpfFact]
    public void A_later_valid_pick_clears_the_message_about_an_invalid_one()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Chord(screen.AddField, Key.LeftCtrl, Key.C);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("", screen.AddMessage);
    }
}
