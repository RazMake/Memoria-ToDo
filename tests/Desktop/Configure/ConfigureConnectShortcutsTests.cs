using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>Connecting the two shortcut actions: each registers its default at once, and the answer says whether both did.</summary>
[Unit]
public class ConfigureConnectShortcutsTests
{
    private static IConfigureWindow NewConfigure() => new ConfigureServices().Create();

    [Fact]
    public void Registers_Win_Alt_N_on_the_Add_new_task_action_once()
    {
        var addNewTask = new FakeShortcutAction();

        NewConfigure().ConnectShortcuts(addNewTask, new FakeShortcutAction());

        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], addNewTask.Calls);
    }

    [Fact]
    public void Registers_Win_Alt_V_on_the_Manage_tasks_action_once()
    {
        var manageTasks = new FakeShortcutAction();

        NewConfigure().ConnectShortcuts(new FakeShortcutAction(), manageTasks);

        Assert.Equal([ShortcutCall.Register(TestShortcut.ManageTasksDefault)], manageTasks.Calls);
    }

    [Fact]
    public void Returns_true_when_both_defaults_registered()
    {
        var connected = NewConfigure().ConnectShortcuts(new FakeShortcutAction(), new FakeShortcutAction());

        Assert.True(connected);
    }

    [Fact]
    public void Returns_false_when_the_Add_new_task_default_did_not_register_and_still_tries_Manage_tasks()
    {
        var manageTasks = new FakeShortcutAction();

        var connected = NewConfigure().ConnectShortcuts(new FakeShortcutAction { Answer = _ => false }, manageTasks);

        Assert.False(connected);
        Assert.Single(manageTasks.Calls);
    }

    [Fact]
    public void Returns_false_when_the_Manage_tasks_default_did_not_register_and_still_tries_Add_new_task()
    {
        var addNewTask = new FakeShortcutAction();

        var connected = NewConfigure().ConnectShortcuts(addNewTask, new FakeShortcutAction { Answer = _ => false });

        Assert.False(connected);
        Assert.Single(addNewTask.Calls);
    }

    [Fact]
    public void Returns_false_when_the_Add_new_task_action_throws_and_still_tries_Manage_tasks()
    {
        var manageTasks = new FakeShortcutAction();
        var throwing = new FakeShortcutAction { RegisterFailure = new InvalidOperationException("Windows refused.") };

        var connected = NewConfigure().ConnectShortcuts(throwing, manageTasks);

        Assert.False(connected);
        Assert.Single(manageTasks.Calls);
    }

    [Fact]
    public void Returns_false_when_the_Manage_tasks_action_throws_and_still_tries_Add_new_task()
    {
        var addNewTask = new FakeShortcutAction();
        var throwing = new FakeShortcutAction { RegisterFailure = new InvalidOperationException("Windows refused.") };

        var connected = NewConfigure().ConnectShortcuts(addNewTask, throwing);

        Assert.False(connected);
        Assert.Single(addNewTask.Calls);
    }

    [Fact]
    public void Rejects_a_missing_action()
    {
        var configure = NewConfigure();

        Assert.Throws<ArgumentNullException>(() => configure.ConnectShortcuts(null!, new FakeShortcutAction()));
        Assert.Throws<ArgumentNullException>(() => configure.ConnectShortcuts(new FakeShortcutAction(), null!));
    }
}
