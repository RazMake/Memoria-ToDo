using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>Connecting the two shortcut actions on the real Configure window. The per-row rules are unit-tested on ShortcutSetting.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureConnectShortcutsTests
{
    [WpfFact]
    public void Connecting_registers_all_three_defaults_and_still_tries_the_others_when_one_fails()
    {
        var addNewTask = new FakeShortcutAction { Answer = _ => false };
        var manageTasks = new FakeShortcutAction();
        var configure = new FakeShortcutAction();

        var connected = new ConfigureServices().Create().ConnectShortcuts(addNewTask, manageTasks, configure);

        Assert.False(connected);
        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], addNewTask.Calls);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ManageTasksDefault)], manageTasks.Calls);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ConfigureDefault)], configure.Calls);
    }

    [WpfFact]
    public void Connecting_registers_the_Configure_default_and_answers_true_when_all_three_registered()
    {
        var configure = new FakeShortcutAction();

        var connected = new ConfigureServices().Create()
            .ConnectShortcuts(new FakeShortcutAction(), new FakeShortcutAction(), configure);

        Assert.True(connected);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ConfigureDefault)], configure.Calls);
    }

    [WpfFact]
    public void Connecting_answers_false_when_only_the_Configure_shortcut_did_not_register()
    {
        var connected = new ConfigureServices().Create().ConnectShortcuts(
            new FakeShortcutAction(), new FakeShortcutAction(), new FakeShortcutAction { Answer = _ => false });

        Assert.False(connected);
    }

    [WpfFact]
    public void Connecting_answers_false_when_the_Configure_action_throws_and_still_registers_the_others()
    {
        var addNewTask = new FakeShortcutAction();
        var manageTasks = new FakeShortcutAction();

        var connected = new ConfigureServices().Create().ConnectShortcuts(
            addNewTask, manageTasks, new FakeShortcutAction { RegisterFailure = new InvalidOperationException("Windows failed.") });

        Assert.False(connected);
        Assert.Single(addNewTask.Calls);
        Assert.Single(manageTasks.Calls);
    }

    [WpfFact]
    public void Connecting_registers_the_stored_Configure_shortcut_instead_of_the_default()
    {
        var services = new ConfigureServices();
        services.Store.Settings.SetConfigureShortcut("Ctrl+Alt+K");
        var configure = new FakeShortcutAction();

        services.Create().ConnectShortcuts(new FakeShortcutAction(), new FakeShortcutAction(), configure);

        Assert.Equal([ShortcutCall.Register(TestShortcut.A)], configure.Calls);
    }

    [WpfFact]
    public void Connecting_rejects_a_missing_action()
    {
        using var configureWindow = new ConfigureServices().Create();

        Assert.Equal("addNewTask", Assert.Throws<ArgumentNullException>(() =>
            configureWindow.ConnectShortcuts(null!, new FakeShortcutAction(), new FakeShortcutAction())).ParamName);
        Assert.Equal("manageTasks", Assert.Throws<ArgumentNullException>(() =>
            configureWindow.ConnectShortcuts(new FakeShortcutAction(), null!, new FakeShortcutAction())).ParamName);
        Assert.Equal("configure", Assert.Throws<ArgumentNullException>(() =>
            configureWindow.ConnectShortcuts(new FakeShortcutAction(), new FakeShortcutAction(), null!)).ParamName);
    }
}
