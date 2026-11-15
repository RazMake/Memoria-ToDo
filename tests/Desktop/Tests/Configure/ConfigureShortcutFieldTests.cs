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
        Assert.Equal("", screen.AddMessage);
        Assert.Equal("", screen.ManageMessage);
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
