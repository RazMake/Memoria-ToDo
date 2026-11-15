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
    public void Connecting_registers_both_defaults_and_still_tries_the_other_when_one_fails()
    {
        var addNewTask = new FakeShortcutAction { Answer = _ => false };
        var manageTasks = new FakeShortcutAction();

        var connected = new ConfigureServices().Create().ConnectShortcuts(addNewTask, manageTasks);

        Assert.False(connected);
        Assert.Equal([ShortcutCall.Register(TestShortcut.AddNewTaskDefault)], addNewTask.Calls);
        Assert.Equal([ShortcutCall.Register(TestShortcut.ManageTasksDefault)], manageTasks.Calls);
    }
}
