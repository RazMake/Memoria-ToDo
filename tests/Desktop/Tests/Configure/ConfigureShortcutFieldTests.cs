using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The two shortcut rows on screen: the common paths only. The rules, messages and registration order are
/// unit-tested on ShortcutSetting (ShortcutSetting*Tests, ShortcutTextTests).
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureShortcutFieldTests
{
    [WpfFact]
    public void Show_the_default_of_each_row_in_its_box_with_no_message()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal("Win+Alt+N", screen.AddField.Text);
        Assert.Equal("Win+Alt+V", screen.ManageField.Text);
        Assert.Equal("Win+Alt+C", screen.ConfigureField.Text);
        Assert.Equal("", screen.AddMessage);
        Assert.Equal("", screen.ManageMessage);
        Assert.Equal("", screen.ConfigureMessage);
    }

    [WpfFact]
    public void A_Configure_default_that_did_not_register_leaves_its_box_empty_and_says_why()
    {
        using var screen = new ConfigureScreen(configure: new FakeShortcutAction { Answer = _ => false });

        screen.Open();

        Assert.Equal("", screen.ConfigureField.Text);
        Assert.Contains("Win+Alt+C", screen.ConfigureMessage);
        Assert.Equal("Win+Alt+N", screen.AddField.Text);
    }

    [WpfFact]
    public void An_accepted_Configure_pick_registers_through_the_Configure_action_and_shows_in_its_box()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ConfigureField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("Ctrl+Shift+K", screen.ConfigureField.Text);
        Assert.Equal("Win+Alt+N", screen.AddField.Text);
        Assert.Contains(
            ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Shift, Key.K)),
            screen.ConfigureAction.Calls);
    }

    [WpfFact]
    public void A_Configure_pick_taken_by_another_app_says_so_and_keeps_the_previous_shortcut()
    {
        using var screen = new ConfigureScreen(configure: new FakeShortcutAction
        {
            Answer = shortcut => shortcut == TestShortcut.ConfigureDefault,
        });
        screen.Open();

        screen.Chord(screen.ConfigureField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Contains("Ctrl+Shift+K", screen.ConfigureMessage);
        Assert.Equal("Win+Alt+C", screen.ConfigureField.Text);
        Assert.Equal(TestShortcut.ConfigureDefault, screen.ConfigureAction.Registered);
    }

    [WpfFact]
    public void A_Configure_pick_with_one_modifier_is_rejected_and_keeps_the_shortcut()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ConfigureField, Key.LeftCtrl, Key.C);

        Assert.Contains("two or more modifier keys", screen.ConfigureMessage);
        Assert.Equal("Win+Alt+C", screen.ConfigureField.Text);
    }

    [WpfFact]
    public void A_Configure_pick_equal_to_another_shortcut_is_rejected_and_keeps_the_shortcut()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ConfigureField, Key.LWin, Key.LeftAlt, Key.N);

        Assert.NotEqual("", screen.ConfigureMessage);
        Assert.Equal("Win+Alt+C", screen.ConfigureField.Text);
        Assert.Equal("Win+Alt+N", screen.AddField.Text);
    }

    [WpfFact]
    public void A_pick_on_another_row_equal_to_the_Configure_shortcut_is_rejected_naming_Configure()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LWin, Key.LeftAlt, Key.C);

        Assert.Contains("Configure", screen.AddMessage);
        Assert.Equal("Win+Alt+N", screen.AddField.Text);
        Assert.Equal("Win+Alt+C", screen.ConfigureField.Text);
    }

    [WpfFact]
    public void A_default_that_did_not_register_leaves_its_box_empty_and_says_why()
    {
        using var screen = new ConfigureScreen(addNewTask: new FakeShortcutAction { Answer = _ => false });

        screen.Open();

        Assert.Equal("", screen.AddField.Text);
        Assert.Contains("Win+Alt+N", screen.AddMessage);
        Assert.Equal("Win+Alt+V", screen.ManageField.Text);
    }

    [WpfFact]
    public void An_accepted_pick_registers_and_shows_in_its_rows_box()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift, Key.K);

        Assert.Equal("Ctrl+Shift+K", screen.AddField.Text);
        Assert.Equal("Win+Alt+V", screen.ManageField.Text);
        Assert.Contains(
            ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Shift, Key.K)),
            screen.AddNewTask.Calls);
    }

    [WpfFact]
    public void A_pick_with_one_modifier_shows_why_under_its_box_and_keeps_the_shortcut()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.C);

        Assert.Contains("two or more modifier keys", screen.ManageMessage);
        Assert.Equal("", screen.AddMessage);
        Assert.Equal("Win+Alt+V", screen.ManageField.Text);
    }
}
