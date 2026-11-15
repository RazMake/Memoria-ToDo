using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Esc in the Configure window on screen: it closes the window as the close button does, after an open menu. Esc in a
/// shortcut box is in ConfigureCloseWindowTests. The platform's EscCloses part and ShortcutKeyInput have their own unit tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureEscTests
{
    [WpfFact]
    public void Esc_drops_a_typed_repository_address_like_the_close_button_does()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.ChooseRepositoryAction("Clone a repo");
        var address = screen.Box("Repository address");
        ConfigureScreen.Focus(address);
        address.Text = "git@example.com:tasks.git";

        screen.PressEscape(address);

        Assert.False(screen.IsShowing);
        screen.Open();
        screen.ClickTitle("Sync settings");
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
        screen.ClickTitle("Sync settings");
        screen.Click("Sync");
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

        screen.PressEscape(menu);

        Assert.False(menu.IsOpen);
        Assert.True(screen.IsShowing);

        OpenWindows.PressEscape(screen.Window);

        Assert.False(screen.IsShowing);
    }
}
