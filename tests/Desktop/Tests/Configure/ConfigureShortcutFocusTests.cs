using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// While a shortcut box has the focus, both shortcuts are released; when the focus leaves the boxes, each is
/// registered again. The rules are unit-tested on ShortcutFocus; this checks that real focus drives it.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureShortcutFocusTests
{
    [WpfFact]
    public void Focus_entering_a_box_releases_the_shortcuts_and_focus_leaving_the_boxes_registers_them_again()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        Assert.Empty(screen.AddNewTask.Calls);

        ConfigureScreen.Focus(screen.AddField);
        ConfigureScreen.Focus(screen.ManageField);

        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ManageTasks.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ConfigureAction.Calls);

        screen.FocusStartWithWindowsBox();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ConfigureDefault), screen.ConfigureAction.Calls[^1]);
    }

    [WpfFact]
    public void Focus_entering_the_Configure_box_releases_all_three_global_shortcuts_and_leaving_registers_them_again()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        ConfigureScreen.Focus(screen.ConfigureField);

        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ManageTasks.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ConfigureAction.Calls);

        screen.FocusStartWithWindowsBox();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ConfigureDefault), screen.ConfigureAction.Calls[^1]);
    }

    [WpfFact]
    public void Focus_entering_the_Close_window_box_releases_all_three_global_shortcuts()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        ConfigureScreen.Focus(screen.CloseField);

        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ManageTasks.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ConfigureAction.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ContactsAction.Calls);

        screen.FocusStartWithWindowsBox();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ConfigureDefault), screen.ConfigureAction.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ContactsDefault), screen.ContactsAction.Calls[^1]);
    }

    [WpfFact]
    public void Focus_entering_the_Contacts_box_releases_all_four_global_shortcuts_and_leaving_registers_them_again()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        ConfigureScreen.Focus(screen.ContactsField);

        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ManageTasks.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ConfigureAction.Calls);
        Assert.Equal([ShortcutCall.Unregister()], screen.ContactsAction.Calls);

        screen.FocusStartWithWindowsBox();

        Assert.Equal(ShortcutCall.Register(TestShortcut.ContactsDefault), screen.ContactsAction.Calls[^1]);
    }

    [WpfFact]
    public void Showing_Configure_again_registers_each_global_shortcut_again_and_a_Contacts_shortcut_lost_meanwhile_shows_as_taken()
    {
        var contacts = new FakeShortcutAction();
        using var screen = new ConfigureScreen(contacts: contacts);
        screen.Open();
        screen.CloseWindow();
        contacts.Answer = _ => false;

        screen.Show();

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ConfigureDefault), screen.ConfigureAction.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ContactsDefault), contacts.Calls[^1]);
        Assert.Equal("", screen.ContactsField.Text);
        Assert.NotEqual("", screen.ContactsMessage);
    }
}
