using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Esc in the Configure window on screen: it closes the window as the close button does, after an open menu, and
/// plain Esc does the same from a shortcut box. The platform's EscCloses part and ShortcutKeyInput have their own unit tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureEscTests
{
    [WpfFact]
    public void Esc_closes_the_window()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        OpenWindows.PressEscape(screen.Window);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Esc_drops_a_typed_repository_address_like_the_close_button_does()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Expand("Sync settings");
        screen.ChooseRepositoryAction("Clone a repo");
        var address = screen.Box("Repository address");
        ConfigureScreen.Focus(address);
        address.Text = "git@example.com:tasks.git";

        ConfigureScreen.PressEscape(address);

        Assert.False(screen.IsShowing);
        screen.Open();
        screen.Expand("Sync settings");
        screen.ChooseRepositoryAction("Clone a repo");
        Assert.Equal("", screen.Box("Repository address").Text);
        Assert.Empty(screen.Sync.SetupControl.Clones);
    }

    [WpfFact]
    public void Esc_lets_a_running_sync_go_on_like_the_close_button_does()
    {
        using var screen = new ConfigureScreen();
        screen.Sync.SyncOperation = _ => new TaskCompletionSource<Memoria.ToDo.Shared.Sync.ISyncStatus>().Task;
        screen.Open();
        screen.Expand("Sync settings");
        screen.Click("Sync now");
        Assert.Equal(1, screen.Sync.SyncCount);

        OpenWindows.PressEscape(screen.Window);

        Assert.False(screen.IsShowing);
        Assert.Equal(0, screen.Sync.StopCount);
        Assert.Equal(0, screen.Sync.DisposeCount);
    }

    [WpfFact]
    public void Esc_closes_an_open_menu_first_and_a_second_Esc_closes_the_window()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var menu = screen.OpenRepositoryMenu();

        ConfigureScreen.PressEscape(menu);

        Assert.False(menu.IsOpen);
        Assert.True(screen.IsShowing);

        OpenWindows.PressEscape(screen.Window);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Plain_Esc_in_a_shortcut_box_closes_the_window_keeps_the_shortcut_and_registers_the_shortcuts_again()
    {
        var add = new FakeShortcutAction();
        var manage = new FakeShortcutAction();
        using var screen = new ConfigureScreen(addNewTask: add, manageTasks: manage,
            initialSettings: settings => settings.SetAddNewTaskShortcut("Ctrl+Alt+K"));
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);
        Assert.Equal([ShortcutCall.Unregister()], add.Calls);

        ConfigureScreen.PressEscape(screen.AddField);

        Assert.False(screen.IsShowing);
        Assert.Equal("Ctrl+Alt+K", screen.Store.Settings.AddNewTaskShortcut);
        Assert.Equal(
            ShortcutCall.Register(TestShortcut.Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K)), add.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), manage.Calls[^1]);
    }

    [WpfFact]
    public void Esc_with_a_modifier_held_in_a_shortcut_box_picks_nothing_and_the_window_stays()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.Escape);

        Assert.True(screen.IsShowing);
        Assert.Equal("Win+Alt+N", screen.AddField.Text);
        Assert.Equal("", screen.AddMessage);
        Assert.Equal([ShortcutCall.Unregister()], screen.AddNewTask.Calls);
    }
}
