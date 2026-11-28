using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// A global shortcut's pick must differ from the Save keys too: a global shortcut takes its keys before any window
/// sees them, so Save would never work. The message names the row that uses the keys.
/// </summary>
[Unit]
public class ShortcutSettingSaveTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;
    private const ModifierKeys Shift = ModifierKeys.Shift;

    private readonly TestShortcutSetting add = TestShortcutSetting.AddNewTask().Connected();
    private readonly TestShortcutSetting manage = TestShortcutSetting.ManageTasks().Connected();
    private readonly TestSaveSetting save = new() { Stored = "Ctrl+Shift+K" };

    public ShortcutSettingSaveTests() => save.Create();

    [Fact]
    public void A_pick_equal_to_the_Save_keys_names_Save_and_changes_nothing()
    {
        add.Setting.Pick(Ctrl | Shift, Key.K, manage.Setting, save.Setting);

        Assert.Equal("Save already uses Ctrl+Shift+K. Press a different shortcut.", add.Setting.Message);
        Assert.Empty(add.Action.Calls);
        Assert.Empty(add.Saved);
        Assert.Equal(TestShortcut.AddNewTaskDefault, add.Setting.Active);
    }

    [Fact]
    public void Task_View_is_refused_the_Save_keys_too()
    {
        manage.Setting.Pick(Ctrl | Shift, Key.K, add.Setting, save.Setting);

        Assert.Equal("Save already uses Ctrl+Shift+K. Press a different shortcut.", manage.Setting.Message);
        Assert.Empty(manage.Action.Calls);
    }

    [Fact]
    public void A_pick_equal_to_the_Save_keys_the_user_changed_is_refused()
    {
        save.Setting.Pick(Ctrl | Alt, Key.K, add.Setting, manage.Setting);

        add.Setting.Pick(Ctrl | Alt, Key.K, manage.Setting, save.Setting);

        Assert.Equal("Save already uses Ctrl+Alt+K. Press a different shortcut.", add.Setting.Message);
    }

    [Fact]
    public void A_pick_that_differs_from_every_row_registers_and_is_saved()
    {
        add.Setting.Pick(Ctrl | Alt, Key.K, manage.Setting, save.Setting);

        Assert.Equal([ShortcutCall.Register(TestShortcut.Of(Ctrl | Alt, Key.K))], add.Action.Calls);
        Assert.Equal(["Ctrl+Alt+K"], add.Saved);
        Assert.Null(add.Setting.Message);
    }
}
