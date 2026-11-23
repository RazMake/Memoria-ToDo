using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Configure on top of the shared settings, with the window created but never shown: the Save keys it gives the app's
/// windows, and the shortcuts it registers again when another thread changes a setting. The rules per shortcut are in
/// ShortcutSettingSettingsChangedTests and WindowKeysSettingTests.
/// </summary>
[Unit]
public class ConfigureWindowSettingsChangedTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    private static async Task PumpAsync() =>
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

    [WpfFact]
    public void The_Save_keys_are_Ctrl_plus_Enter_as_soon_as_Configure_is_created()
    {
        var services = new ConfigureServices();
        using var window = services.Create();

        Assert.True(services.SaveKeys.Matches(Ctrl, Key.Enter));
    }

    [WpfFact]
    public void Stored_Save_text_is_applied_to_the_Save_keys_when_Configure_is_created()
    {
        var services = new ConfigureServices();
        services.Store.Settings.SetSaveShortcut("Ctrl+Alt+K");
        using var window = services.Create();

        Assert.True(services.SaveKeys.Matches(Ctrl | Alt, Key.K));
        Assert.False(services.SaveKeys.Matches(Ctrl, Key.Enter));
    }

    [WpfFact]
    public async Task A_Save_change_in_the_settings_applies_to_the_Save_keys_while_the_window_is_closed()
    {
        var services = new ConfigureServices();
        using var window = services.Create();

        await Task.Run(() => services.Store.Settings.SetSaveShortcut("Ctrl+Alt+K"));
        await PumpAsync();

        Assert.True(services.SaveKeys.Matches(Ctrl | Alt, Key.K));
    }

    [WpfFact]
    public async Task An_invalid_Save_change_in_the_settings_keeps_the_previous_keys_and_is_not_repaired()
    {
        var services = new ConfigureServices();
        services.Store.Settings.SetSaveShortcut("Ctrl+Alt+K");
        using var window = services.Create();

        await Task.Run(() => services.Store.Settings.SetSaveShortcut("not a Windows shortcut"));
        await PumpAsync();

        Assert.True(services.SaveKeys.Matches(Ctrl | Alt, Key.K));
        Assert.Equal("not a Windows shortcut", services.Store.Settings.SaveShortcut);
    }

    [WpfFact]
    public void Invalid_stored_Save_text_keeps_Ctrl_plus_Enter_and_is_not_repaired()
    {
        var services = new ConfigureServices();
        services.Store.Settings.SetSaveShortcut("not a Windows shortcut");
        using var window = services.Create();

        Assert.True(services.SaveKeys.Matches(Ctrl, Key.Enter));
        Assert.Equal("not a Windows shortcut", services.Store.Settings.SaveShortcut);
    }

    [WpfFact]
    public async Task A_Configure_change_from_a_worker_thread_updates_the_registration_even_while_the_window_is_closed()
    {
        var services = new ConfigureServices();
        var configure = new FakeShortcutAction();
        using var window = services.Create();
        window.ConnectShortcuts(new FakeShortcutAction(), new FakeShortcutAction(), configure, new FakeShortcutAction());

        await Task.Run(() => services.Store.Settings.SetConfigureShortcut("Ctrl+Alt+K"));
        await PumpAsync();

        Assert.Equal(ShortcutCall.Register(TestShortcut.A), configure.Calls.Last());
    }

    [WpfFact]
    public async Task A_Task_View_change_from_a_worker_thread_updates_the_registration_even_while_the_window_is_closed()
    {
        var services = new ConfigureServices();
        var manageTasks = new FakeShortcutAction();
        using var window = services.Create();
        window.ConnectShortcuts(new FakeShortcutAction(), manageTasks, new FakeShortcutAction(), new FakeShortcutAction());

        await Task.Run(() => services.Store.Settings.SetManageTasksShortcut("Ctrl+Alt+K"));
        await PumpAsync();

        Assert.Equal(ShortcutCall.Register(TestShortcut.A), manageTasks.Calls.Last());
    }

    [WpfFact]
    public async Task Disposing_Configure_detaches_settings_notices()
    {
        var services = new ConfigureServices();
        var addNewTask = new FakeShortcutAction();
        var manageTasks = new FakeShortcutAction();
        var window = services.Create();
        window.ConnectShortcuts(addNewTask, manageTasks, new FakeShortcutAction(), new FakeShortcutAction());
        window.Dispose();
        addNewTask.ClearCalls();
        manageTasks.ClearCalls();

        services.Store.Settings.SetManageTasksShortcut("Ctrl+Alt+K");
        await PumpAsync();

        Assert.Empty(addNewTask.Calls);
        Assert.Empty(manageTasks.Calls);
    }
}
