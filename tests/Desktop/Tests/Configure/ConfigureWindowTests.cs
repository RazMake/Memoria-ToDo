using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

[UX]
[Collection(ScreenTests.Collection)]
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
    public void Shows_three_sections_in_order_with_only_Shortcuts_open()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(
            ["Shortcuts", "Startup", "Sync settings"],
            screen.Sections.Select(section => ((TextBlock)section.Header).Text));
        Assert.Equal(["Shortcuts"], screen.OpenSections);
    }

    [WpfTheory]
    [InlineData("Shortcuts")]
    [InlineData("Startup")]
    [InlineData("Sync settings")]
    public void Collapsing_the_open_section_leaves_it_open_and_opens_no_other(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Expand(heading);

        screen.Collapse(heading);

        Assert.Equal([heading], screen.OpenSections);
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
    public void Has_a_fixed_size_of_800_by_850_and_cannot_be_resized()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(ResizeMode.NoResize, screen.Window.ResizeMode);
        Assert.Equal(SizeToContent.Manual, screen.Window.SizeToContent);
        // Windows caps a window's height at the tallest it allows on the screen.
        Assert.Equal(
            new Size(800, Math.Min(850, SystemParameters.MaximumWindowTrackHeight)),
            new Size(screen.Window.Width, screen.Window.Height));
    }

    [WpfTheory]
    [InlineData("Startup")]
    [InlineData("Sync settings")]
    public void Keeps_its_size_when_another_section_is_opened(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var before = screen.WindowSize;

        screen.Expand(heading);

        Assert.Equal(before, screen.WindowSize);
    }

    [WpfTheory]
    [InlineData("Shortcuts")]
    [InlineData("Startup")]
    public void Keeps_the_headings_below_the_open_section_at_the_bottom(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Expand(heading);

        _ = screen.WindowSize;
        var content = (FrameworkElement)screen.Window.Content;
        var last = screen.Sections[^1];
        Assert.Equal(content.ActualHeight, last.TranslatePoint(new Point(0, last.ActualHeight), content).Y, 1);
    }

    [WpfFact]
    public void Keeps_its_size_when_a_long_message_appears()
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
        Assert.Equal(before, screen.WindowSize);
    }
}
