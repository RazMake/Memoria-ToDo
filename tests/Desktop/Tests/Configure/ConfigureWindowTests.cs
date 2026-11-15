using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
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
    public void Shows_three_section_titles_in_order_with_only_Shortcuts_open()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(["Shortcuts", "Startup", "Sync settings"], screen.Titles.Select(ScreenTitle));
        Assert.Equal(["Shortcuts"], screen.OpenSections);
    }

    [WpfTheory]
    [InlineData("Startup")]
    [InlineData("Sync settings")]
    public void Clicking_a_closed_sections_title_opens_it_and_closes_the_other(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.ClickTitle(heading);

        Assert.Equal([heading], screen.OpenSections);
    }

    [WpfTheory]
    [InlineData("Shortcuts")]
    [InlineData("Startup")]
    [InlineData("Sync settings")]
    public void Clicking_the_open_sections_title_does_nothing(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.ClickTitle(heading);

        screen.ClickTitle(heading);

        Assert.Equal([heading], screen.OpenSections);
    }

    [WpfFact]
    public void Shows_no_expand_or_collapse_toggle_on_a_section()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Empty(OpenWindows.Descendants(screen.Window).OfType<Expander>());
    }

    [WpfFact]
    public void Tab_reaches_a_section_title()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var shortcuts = screen.Title("Shortcuts");
        var startup = screen.Title("Startup");
        ConfigureScreen.Focus(shortcuts);

        for (var steps = 0; steps < 10 && !startup.IsKeyboardFocused; steps++)
        {
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        Assert.True(startup.IsKeyboardFocused);
    }

    [WpfTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void Enter_or_Space_on_a_focused_title_opens_its_section(Key key)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var title = screen.Title("Startup");
        ConfigureScreen.Focus(title);

        SimulatedKey.Chord(title, key);

        Assert.Equal(["Startup"], screen.OpenSections);
    }

    [WpfTheory]
    [InlineData("Shortcuts")]
    [InlineData("Startup")]
    [InlineData("Sync settings")]
    public void The_open_sections_content_starts_right_under_its_title(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        // The app's Fluent styles, which center some controls, must not move the content off its title.
        OpenWindows.UseTheme(screen.Window, dark: false);
        screen.ClickTitle(heading);
        _ = screen.WindowSize;
        var title = screen.Title(heading);
        UIElement first = heading switch
        {
            "Shortcuts" => OpenWindows.Descendants(screen.Window).OfType<TextBlock>().First(b => OpenWindows.TextOf(b).StartsWith("Click a box")),
            "Startup" => screen.StartWithWindowsBox,
            _ => OpenWindows.Descendants(screen.Window).OfType<TextBlock>().First(b => OpenWindows.TextOf(b) == "Repository"),
        };

        var gap = first.TranslatePoint(new Point(0, 0), screen.Window).Y
            - title.TranslatePoint(new Point(0, title.ActualHeight), screen.Window).Y;

        Assert.InRange(gap, 0, 40);
    }

    [WpfTheory]
    [InlineData("Shortcuts", false)]
    [InlineData("Startup", false)]
    [InlineData("Sync settings", false)]
    [InlineData("Shortcuts", true)]
    [InlineData("Startup", true)]
    [InlineData("Sync settings", true)]
    public void Shows_each_section_title_in_heading_1s_color_bold_and_size_for_the_theme(string heading, bool dark)
    {
        using var screen = new ConfigureScreen();
        screen.Theme.IsDark = dark;
        screen.Open();
        _ = screen.WindowSize;

        var text = TitleBlock(screen, heading);

        Assert.Equal(HeadingColor(dark), ((SolidColorBrush)text.Foreground).Color);
        Assert.Equal(FontWeights.Bold, text.FontWeight);
        Assert.Equal(TextLooks.HeadingScale(1) * NormalTextSize(screen), text.FontSize);
    }

    [WpfFact]
    public void Switches_every_section_title_to_the_new_themes_heading_1_color_when_the_theme_changes()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        _ = screen.WindowSize;

        screen.Theme.Switch(isDark: true);
        var dark = ConfigureScreen.SectionHeadings.Select(h => ((SolidColorBrush)TitleBlock(screen, h).Foreground).Color).ToList();
        screen.Theme.Switch(isDark: false);
        var light = ConfigureScreen.SectionHeadings.Select(h => ((SolidColorBrush)TitleBlock(screen, h).Foreground).Color).ToList();

        Assert.All(dark, color => Assert.Equal(HeadingColor(true), color));
        Assert.All(light, color => Assert.Equal(HeadingColor(false), color));
    }

    [WpfFact]
    public void Shows_each_section_and_each_sync_card_as_a_rounded_box()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.Window.UpdateLayout();

        Assert.True(RoundedBoxes(screen.Box("Device name")) >= 2, "Git Access sits in a card inside the section.");
        screen.ClickTitle("Shortcuts");
        Assert.True(RoundedBoxes(screen.AddField) >= 1, "The shortcut rows sit in a section box.");
    }

    [WpfFact]
    public void Shows_the_instruction_line_in_normal_text_not_the_help_style()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var help = new Style(typeof(TextBlock));
        help.Setters.Add(new Setter(TextBlock.FontSizeProperty, 7.0));
        screen.Window.Resources["CaptionTextBlockStyle"] = help;
        screen.Window.Resources["TextFillColorSecondaryBrush"] = Brushes.Red;

        var line = OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .Single(b => OpenWindows.TextOf(b) == "Click a box, then press the new shortcut. Esc keeps the current one:");

        Assert.NotEqual(7.0, line.FontSize);
        Assert.NotSame(Brushes.Red, line.Foreground);
        Assert.Equal(1.0, line.Opacity);
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
        Assert.Same(themeTextBox, screen.CloseField.Style);
    }

    [WpfFact]
    public void Has_a_fixed_size_of_800_by_850_and_cannot_be_resized()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        Assert.Equal(ResizeMode.NoResize, screen.Window.ResizeMode);
        Assert.Equal(SizeToContent.Manual, screen.Window.SizeToContent);
        // Windows caps a window's size at the largest it allows on the screen.
        var width = Math.Min(800, SystemParameters.MaximumWindowTrackWidth);
        var height = Math.Min(850, SystemParameters.MaximumWindowTrackHeight);
        Assert.Equal(width, screen.Window.Width, 0.01);
        Assert.Equal(height, screen.Window.Height, 0.01);
    }

    [WpfTheory]
    [InlineData("Startup")]
    [InlineData("Sync settings")]
    public void Keeps_its_size_when_another_section_is_opened(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var before = screen.WindowSize;

        screen.ClickTitle(heading);

        Assert.Equal(before, screen.WindowSize);
    }

    [WpfTheory]
    [InlineData("Shortcuts")]
    [InlineData("Startup")]
    public void Keeps_the_titles_below_the_open_section_at_the_bottom(string heading)
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.ClickTitle(heading);

        _ = screen.WindowSize;
        var content = (FrameworkElement)screen.Window.Content;
        var last = screen.Title("Sync settings");
        var bottom = last.TranslatePoint(new Point(0, last.ActualHeight), content).Y;
        const double WindowMargin = 24;
        Assert.InRange(content.ActualHeight - bottom, 0, WindowMargin + 30);
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
        screen.ClickTitle("Startup");
        var before = screen.WindowSize;

        screen.SwitchStartWithWindows();

        Assert.True(screen.ShowsText(twoLineError));
        Assert.Equal(before, screen.WindowSize);
    }

    private static string ScreenTitle(ContentControl title) =>
        title.Content as string ?? OpenWindows.TextOf((TextBlock)title.Content);

    // The text block that draws the title's heading, whether the title's content is a string or a text block.
    private static TextBlock TitleBlock(ConfigureScreen screen, string heading) =>
        VisualTextBlocks(screen.Title(heading)).First();

    private static IEnumerable<TextBlock> VisualTextBlocks(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock block)
            {
                yield return block;
            }

            foreach (var inner in VisualTextBlocks(child))
            {
                yield return inner;
            }
        }
    }

    private static Color HeadingColor(bool dark) =>
        (Color)ColorConverter.ConvertFromString(TextLooks.Of(dark).HeadingColor(1)!);

    // The window's normal text size: the size of the instruction line, which has no style of its own.
    private static double NormalTextSize(ConfigureScreen screen) =>
        OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .First(b => OpenWindows.TextOf(b).StartsWith("Click a box")).FontSize;

    // The rounded boxes (borders with a corner radius) the element sits in.
    private static int RoundedBoxes(DependencyObject element)
    {
        var count = 0;
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is Border { CornerRadius.TopLeft: > 0 })
            {
                count++;
            }
        }

        return count;
    }
}
