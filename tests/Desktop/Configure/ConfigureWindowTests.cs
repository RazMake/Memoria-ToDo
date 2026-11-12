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

        using var configure = new ConfigureServices().Create();

        Assert.Empty(windows.Visible());
    }

    [WpfFact]
    public void Show_opens_the_window_visible_and_active()
    {
        using var windows = new OpenWindows();
        using var configure = new ConfigureServices().Create();

        configure.Show();

        var window = Assert.Single(windows.Visible());
        Assert.True(window.IsActive);
    }

    [WpfFact]
    public void A_second_Show_while_open_brings_the_same_window_forward()
    {
        using var windows = new OpenWindows();
        using var configure = new ConfigureServices().Create();
        configure.Show();
        var first = Assert.Single(windows.Visible());

        configure.Show();

        Assert.Same(first, Assert.Single(windows.Visible()));
        Assert.True(first.IsActive);
    }

    [WpfFact]
    public void Does_not_show_a_line_about_features_not_built_yet()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.DoesNotContain(screen.Texts, text => text.Contains("built yet", StringComparison.OrdinalIgnoreCase));
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
    public void Shows_three_sections_in_order_all_collapsed()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(
            ["Shortcuts", "Startup", "Sync settings"],
            screen.Sections.Select(section => ((TextBlock)section.Header).Text));
        Assert.All(screen.Sections, section => Assert.False(section.IsExpanded));
    }

    [WpfTheory]
    [InlineData("Shortcuts")]
    [InlineData("Startup")]
    [InlineData("Sync settings")]
    public void Expanding_a_section_collapses_every_other_one(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        foreach (var other in screen.Sections)
        {
            other.IsExpanded = true;
        }

        screen.Expand(heading);

        Assert.Equal(
            [heading],
            screen.Sections.Where(section => section.IsExpanded).Select(section => ((TextBlock)section.Header).Text));
    }

    [WpfFact]
    public void Shows_help_under_Start_with_Windows()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.True(screen.ShowsText("when you sign in to Windows"));
    }

    [WpfFact]
    public void Shows_help_texts_partly_transparent()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        var help = OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .Where(block => block.Text.StartsWith("Click a box", StringComparison.Ordinal)
                || block.Text.StartsWith("Starts the app", StringComparison.Ordinal)
                || block.Text.StartsWith("Your tasks are kept", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, help.Count);
        Assert.All(help, block => Assert.InRange(block.Opacity, 0.5, 0.8));
    }

    [WpfFact]
    public void Puts_the_shortcut_boxes_5_apart_when_neither_has_a_message()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Expand("Shortcuts");
        screen.Window.UpdateLayout();

        var add = screen.AddField.TransformToAncestor(screen.Window).Transform(new Point(0, screen.AddField.ActualHeight));
        var manage = screen.ManageField.TransformToAncestor(screen.Window).Transform(new Point(0, 0));

        Assert.Equal(5, manage.Y - add.Y, precision: 1);
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
    public void Keeps_its_width_when_each_kind_of_shortcut_message_appears()
    {
        using var screen = new ConfigureScreen(
            manageTasks: new FakeShortcutAction { Answer = shortcut => shortcut == TestShortcut.ManageTasksDefault });
        screen.Open();
        screen.Expand("Shortcuts");
        var before = screen.WindowSize;

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.C);
        Assert.NotEqual("", screen.AddMessage);
        Assert.Equal(before.Width, screen.WindowSize.Width);

        screen.Chord(screen.ManageField, Key.LeftCtrl, Key.LeftShift, Key.K);
        Assert.NotEqual("", screen.ManageMessage);
        Assert.Equal(before.Width, screen.WindowSize.Width);

        screen.Chord(screen.AddField, Key.LWin, Key.LeftAlt, Key.V);
        Assert.Contains("already uses", screen.AddMessage);
        Assert.Equal(before.Width, screen.WindowSize.Width);

        screen.Chord(screen.ManageField, Key.LWin, Key.LeftAlt);
        Assert.Contains("If you pressed a key with", screen.ManageMessage);
        Assert.Equal(before.Width, screen.WindowSize.Width);
    }

    [WpfFact]
    public void Keeps_its_width_when_the_startup_message_appears()
    {
        // Long enough to wrap to a second line, so it would widen a window that fits its content.
        const string twoLineError =
            "Windows didn't let the app change its startup setting. Check whether a policy or another app manages startup apps, then try again.";
        var port = new FakeStartWithWindows { On = false, SetFailure = new InvalidOperationException(twoLineError) };
        using var screen = new ConfigureScreen(port);
        screen.Open();
        screen.Expand("Shortcuts");
        var before = screen.WindowSize;

        screen.SwitchStartWithWindows();

        Assert.True(screen.ShowsText(twoLineError));
        Assert.Equal(before.Width, screen.WindowSize.Width);
    }
}
