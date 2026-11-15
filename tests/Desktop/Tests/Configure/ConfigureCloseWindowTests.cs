using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Shortcuts section's three rows and how Configure closes: the Close window keys from anywhere in the window, and
/// Esc. The rules are unit-tested on CloseWindowSetting, ShortcutKeyInput and DeviceActions; this checks the wiring.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureCloseWindowTests
{
    [WpfFact]
    public void Shows_three_shortcut_rows_Add_new_task_Manage_tasks_and_Close_window_in_order()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        string[] labels = ["Add new task", "Manage tasks", "Close window"];

        Assert.Equal(labels, screen.Texts.Where(text => labels.Contains(text)));
        Assert.Equal("Shift+Escape", screen.CloseField.Text);
    }

    [WpfFact]
    public void Each_row_describes_its_shortcut_under_the_field_in_italics()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        Assert.Equal("Opens the Add window from any app.", OpenWindows.TextOf(ConfigureScreen.DescriptionUnder(screen.AddField)));
        Assert.Equal("Opens View Tasks from any app.", OpenWindows.TextOf(ConfigureScreen.DescriptionUnder(screen.ManageField)));
        Assert.Equal(
            "Closes the app's window you're in, whatever has the focus.",
            OpenWindows.TextOf(ConfigureScreen.DescriptionUnder(screen.CloseField)));
        Assert.All(
            new[] { screen.AddField, screen.ManageField, screen.CloseField },
            field => Assert.Equal(FontStyles.Italic, ConfigureScreen.DescriptionUnder(field).FontStyle));
    }

    [WpfFact]
    public void The_Shortcuts_instruction_line_reads_exactly_as_the_design_says()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        Assert.Contains("Click a box, then press the new shortcut. Esc keeps the current one:", screen.Texts);
    }

    [WpfFact]
    public void The_Close_window_keys_close_the_window_from_a_focused_shortcut_box()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.CloseField);
        var window = screen.Window;

        screen.Chord(screen.CloseField, Key.LeftShift, Key.Escape);

        Assert.False(screen.IsShowing);
        Assert.False(window.IsVisible);
    }

    [WpfFact]
    public void The_Close_window_keys_close_the_window_whatever_has_the_focus()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.FocusStartWithWindowsBox();

        SimulatedKey.Chord(screen.StartWithWindowsBox, Key.LeftShift, Key.Escape);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Closing_with_the_Close_window_keys_restores_the_global_shortcuts_like_the_close_button()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.CloseField);

        screen.Chord(screen.CloseField, Key.LeftShift, Key.Escape);

        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);
        Assert.Equal(ShortcutCall.Register(TestShortcut.ManageTasksDefault), screen.ManageTasks.Calls[^1]);
    }

    [WpfFact]
    public void Esc_closes_the_window_when_no_control_uses_it()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.FocusStartWithWindowsBox();

        screen.PressEscape(screen.StartWithWindowsBox);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void The_first_Esc_in_a_shortcut_box_keeps_the_shortcut_and_leaves_the_boxes_and_the_second_closes()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        ConfigureScreen.Focus(screen.AddField);

        screen.PressEscape(screen.AddField);

        Assert.True(screen.IsShowing);
        Assert.Equal("Win+Alt+N", screen.AddField.Text);
        Assert.False(screen.AddField.IsKeyboardFocused);
        Assert.Equal(ShortcutCall.Register(TestShortcut.AddNewTaskDefault), screen.AddNewTask.Calls[^1]);

        screen.PressEscape(Keyboard.FocusedElement as UIElement ?? screen.Window);

        Assert.False(screen.IsShowing);
    }
}
