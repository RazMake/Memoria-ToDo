using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

[UX]
public class ConfigureWindowTests
{
    [WpfFact]
    public void Create_opens_no_window()
    {
        using var windows = new OpenWindows();

        ConfigureWindow.Create(new FakeStartWithWindows());

        Assert.Empty(windows.Visible());
    }

    [WpfFact]
    public void Show_opens_the_window_visible_and_active()
    {
        using var windows = new OpenWindows();
        var configure = ConfigureWindow.Create(new FakeStartWithWindows());

        configure.Show();

        var window = Assert.Single(windows.Visible());
        Assert.True(window.IsActive);
    }

    [WpfFact]
    public void A_second_Show_while_open_brings_the_same_window_forward()
    {
        using var windows = new OpenWindows();
        var configure = ConfigureWindow.Create(new FakeStartWithWindows());
        configure.Show();
        var first = Assert.Single(windows.Visible());

        configure.Show();

        Assert.Same(first, Assert.Single(windows.Visible()));
        Assert.True(first.IsActive);
    }

    [WpfFact]
    public void Names_sync_and_the_deletion_delay_as_not_built_yet_on_one_line()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        var line = Assert.Single(screen.Texts, text =>
            text.Contains("sync", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("deletion", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\n', line);
    }

    [WpfFact]
    public void Does_not_say_that_the_shortcut_is_not_saved()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.DoesNotContain(screen.Texts, text =>
            text.Contains("saved", StringComparison.OrdinalIgnoreCase)
            || text.Contains("restart", StringComparison.OrdinalIgnoreCase));
    }

    [WpfFact]
    public void Shows_the_sections_in_order_with_their_texts()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(
            [
                "Shortcuts",
                "Click a box, then press the new shortcut. Esc keeps the current one.",
                "Add new task",
                "Manage tasks",
                "Startup",
                "Start with Windows",
                "Sync and the deletion delay aren't built yet.",
            ],
            screen.Texts);
    }

    [WpfFact]
    public void Shows_each_shortcut_box_240_wide()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(240, screen.AddField.Width);
        Assert.Equal(240, screen.ManageField.Width);
    }

    [WpfFact]
    public void Shows_each_shortcut_box_in_the_themes_text_box_style()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var themeTextBox = new Style(typeof(TextBox));

        screen.Window.Resources[typeof(TextBox)] = themeTextBox;

        Assert.Same(themeTextBox, screen.AddField.Style);
        Assert.Same(themeTextBox, screen.ManageField.Style);
    }

    [WpfFact]
    public void Has_a_fixed_width_of_600_sizes_its_height_to_its_content_and_cannot_be_resized()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(ResizeMode.NoResize, screen.Window.ResizeMode);
        Assert.Equal(SizeToContent.Height, screen.Window.SizeToContent);
        Assert.Equal(600, screen.WindowSize.Width);
    }

    [WpfFact]
    public void Keeps_its_width_and_height_when_each_kind_of_shortcut_message_appears()
    {
        using var screen = new ConfigureScreen(
            manageTasks: new FakeShortcutAction { Answer = shortcut => shortcut == TestShortcut.ManageTasksDefault });
        screen.Open();
        var before = screen.WindowSize;

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.C);
        Assert.NotEqual("", screen.AddMessage);
        Assert.Equal(before, screen.WindowSize);

        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.LeftShift, Key.K);
        Assert.NotEqual("", screen.ManageMessage);
        Assert.Equal(before, screen.WindowSize);

        screen.Chord(screen.AddField, Key.LWin, Key.LeftAlt, Key.LeftShift, Key.T);
        Assert.Contains("already uses", screen.AddMessage);
        Assert.Equal(before, screen.WindowSize);

        screen.Chord(screen.ManageField, Key.LWin, Key.LeftAlt);
        Assert.Contains("If you pressed a key with", screen.ManageMessage);
        Assert.Equal(before, screen.WindowSize);
    }

    [WpfFact]
    public void Keeps_its_width_and_height_when_the_startup_message_appears()
    {
        // Long enough to wrap to a second line, so it needs the room the message keeps while empty.
        const string twoLineError =
            "Windows didn't let the app change its startup setting. Check whether a policy or another app manages startup apps, then try again.";
        var port = new FakeStartWithWindows { On = false, SetFailure = new InvalidOperationException(twoLineError) };
        using var screen = new ConfigureScreen(port);
        screen.Open();
        var before = screen.WindowSize;

        screen.SwitchStartWithWindows();

        Assert.True(screen.ShowsText(twoLineError));
        Assert.Equal(before, screen.WindowSize);
    }
}
