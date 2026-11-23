using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Connecting the four shortcut actions to the Configure window: which defaults or stored shortcuts register, and what
/// connecting answers. The window is created but never shown. The per-row rules are in ShortcutSettingConnectTests.
/// </summary>
[Unit]
public class ConfigureWindowConnectTests
{
    private static bool Connect(
        IConfigureWindow window, FakeShortcutAction addNewTask, FakeShortcutAction manageTasks,
        FakeShortcutAction configure, FakeShortcutAction contacts) =>
        window.ConnectShortcuts(addNewTask, manageTasks, configure, contacts);

    [WpfFact]
    public void Connecting_registers_all_four_defaults_and_still_tries_the_others_when_one_fails()
    {
        var addNewTask = new FakeShortcutAction { Answer = _ => false };
        var manageTasks = new FakeShortcutAction();
        var configure = new FakeShortcutAction();
        var contacts = new FakeShortcutAction();
        using var window = new ConfigureServices().Create();

        var connected = Connect(window, addNewTask, manageTasks, configure, contacts);

        Assert.False(connected);
        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], addNewTask.Calls);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ManageTasksDefault)], manageTasks.Calls);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ConfigureDefault)], configure.Calls);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ContactsDefault)], contacts.Calls);
    }

    [WpfFact]
    public void Connecting_answers_true_when_all_four_registered()
    {
        using var window = new ConfigureServices().Create();

        var connected = Connect(
            window, new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction());

        Assert.True(connected);
    }

    [WpfFact]
    public void Connecting_answers_false_when_only_the_Contacts_shortcut_did_not_register()
    {
        using var window = new ConfigureServices().Create();

        var connected = Connect(
            window, new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction(),
            new FakeShortcutAction { Answer = _ => false });

        Assert.False(connected);
    }

    [WpfFact]
    public void Connecting_answers_false_when_only_the_Configure_shortcut_did_not_register()
    {
        using var window = new ConfigureServices().Create();

        var connected = Connect(
            window, new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction { Answer = _ => false },
            new FakeShortcutAction());

        Assert.False(connected);
    }

    [WpfFact]
    public void Connecting_answers_false_when_the_Contacts_action_throws_and_still_registers_the_others()
    {
        var addNewTask = new FakeShortcutAction();
        var configure = new FakeShortcutAction();
        using var window = new ConfigureServices().Create();

        var connected = Connect(
            window, addNewTask, new FakeShortcutAction(), configure,
            new FakeShortcutAction { RegisterFailure = new InvalidOperationException("Windows failed.") });

        Assert.False(connected);
        Assert.Single(addNewTask.Calls);
        Assert.Single(configure.Calls);
    }

    [WpfFact]
    public void Connecting_answers_false_when_the_Configure_action_throws_and_still_registers_the_others()
    {
        var addNewTask = new FakeShortcutAction();
        var manageTasks = new FakeShortcutAction();
        using var window = new ConfigureServices().Create();

        var connected = Connect(
            window, addNewTask, manageTasks,
            new FakeShortcutAction { RegisterFailure = new InvalidOperationException("Windows failed.") },
            new FakeShortcutAction());

        Assert.False(connected);
        Assert.Single(addNewTask.Calls);
        Assert.Single(manageTasks.Calls);
    }

    [WpfFact]
    public void Connecting_registers_the_stored_Contacts_shortcut_instead_of_the_default()
    {
        var services = new ConfigureServices();
        services.Store.Settings.SetContactsShortcut("Ctrl+Alt+K");
        var contacts = new FakeShortcutAction();
        using var window = services.Create();

        Connect(window, new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction(), contacts);

        Assert.Equal([ShortcutCall.Register(TestShortcut.A)], contacts.Calls);
    }

    [WpfFact]
    public void Connecting_registers_the_stored_Configure_shortcut_instead_of_the_default()
    {
        var services = new ConfigureServices();
        services.Store.Settings.SetConfigureShortcut("Ctrl+Alt+K");
        var configure = new FakeShortcutAction();
        using var window = services.Create();

        Connect(window, new FakeShortcutAction(), new FakeShortcutAction(), configure, new FakeShortcutAction());

        Assert.Equal([ShortcutCall.Register(TestShortcut.A)], configure.Calls);
    }

    [WpfFact]
    public void Connecting_rejects_a_missing_action()
    {
        using var window = new ConfigureServices().Create();

        Assert.Equal("addNewTask", Assert.Throws<ArgumentNullException>(() =>
            window.ConnectShortcuts(null!, new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction())).ParamName);
        Assert.Equal("manageTasks", Assert.Throws<ArgumentNullException>(() =>
            window.ConnectShortcuts(new FakeShortcutAction(), null!, new FakeShortcutAction(), new FakeShortcutAction())).ParamName);
        Assert.Equal("configure", Assert.Throws<ArgumentNullException>(() =>
            window.ConnectShortcuts(new FakeShortcutAction(), new FakeShortcutAction(), null!, new FakeShortcutAction())).ParamName);
        Assert.Equal("contacts", Assert.Throws<ArgumentNullException>(() =>
            window.ConnectShortcuts(new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction(), null!)).ParamName);
    }
}
