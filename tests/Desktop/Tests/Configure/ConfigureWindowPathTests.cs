using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Configure screen's first paths: opening the window and reading its Application section, moving between sections,
/// and the Update card. Each test follows one whole flow. The rules behind each step are unit-tested on the view
/// models and parts (UpdateCardTests, ShortcutSectionTests, AccordionStateTests and the settings tests).
/// </summary>
[UX("Configure")]
[Collection(ScreenTests.Collection)]
public class ConfigureWindowPathTests
{
    [WpfFact]
    public void Opening_Configure_shows_one_fixed_size_window_with_the_Application_section_its_six_shortcut_rows_and_the_AI_assistants_card()
    {
        using var screen = new ConfigureScreen();
        screen.McpServer.Connections = [new("VS Code", "vs code lines"), new("Codex", "codex lines")];
        Assert.False(screen.IsShowing);

        screen.Open();
        var window = screen.Window;
        screen.Show();

        Assert.Same(window, screen.Window);
        Assert.True(window.IsActive);
        Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
        Assert.Equal(["Application", "Startup", "Sync settings"], screen.Titles.Select(title => title.Content as string ?? OpenWindows.TextOf((TextBlock)title.Content)));
        Assert.Equal(["Application"], screen.OpenSections);
        Assert.True(screen.TextTop("Update") < screen.TextTop("Shortcuts"));
        Assert.Contains("Click a box, then press the new shortcut. Esc keeps the current one:", screen.Texts);
        string[] labels = ["Add new task", "Task View", "Contacts", "Configure", "Close window", "Save"];
        // Take only the first six: the Repository card's "Configure" button repeats one label further down.
        Assert.Equal(labels, screen.Texts.Where(text => labels.Contains(text)).Take(labels.Length));
        Assert.Equal(
            ["Win+Alt+N", "Win+Alt+V", "Win+Alt+O", "Win+Alt+C", "Shift+Escape", "Ctrl+Enter"],
            new[] { screen.AddField, screen.ManageField, screen.ContactsField, screen.ConfigureField, screen.CloseField, screen.SaveField }
                .Select(field => field.Text));
        Assert.Equal(
            new[] { "", "", "", "", "", "" },
            new[] { screen.AddMessage, screen.ManageMessage, screen.ContactsMessage, screen.ConfigureMessage, screen.CloseMessage, screen.SaveMessage });
        Assert.Equal("Opens Contacts from any app.", OpenWindows.TextOf(ConfigureScreen.DescriptionUnder(screen.ContactsField)));

        // The AI assistants card is the third card, below Shortcuts and above the Startup title.
        Assert.True(screen.TextTop("Shortcuts") < screen.TextTop("AI assistants"));
        Assert.True(screen.TextTop("AI assistants") < screen.Title("Startup").TranslatePoint(new Point(0, 0), window).Y);
        Assert.Contains("Running. AI assistants on this PC can use Memoria-ToDo.", screen.AiTexts);
        Assert.True(screen.AiBox.IsChecked);
        Assert.Equal(2, screen.AiCopyButtons.Count);
        screen.CopyAi(1);
        Assert.Equal(["codex lines"], screen.SetupActions.CopiedTexts);
        screen.SwitchAi();
        Assert.Equal([false], screen.McpServer.SetOnCalls);
        Assert.Contains("Off. AI assistants can't reach Memoria-ToDo.", screen.AiTexts);
        screen.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public void Moving_between_sections_opens_one_at_a_time_by_mouse_or_keyboard_and_the_titles_follow_the_theme()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        var size = screen.WindowSize;

        screen.ClickTitle("Startup");
        Assert.Equal(["Startup"], screen.OpenSections);
        screen.ClickTitle("Startup");
        Assert.Equal(["Startup"], screen.OpenSections);
        screen.ClickTitle("Sync settings");
        Assert.Equal(["Sync settings"], screen.OpenSections);
        Assert.Equal(size, screen.WindowSize);

        var startup = screen.Title("Startup");
        ConfigureScreen.Focus(screen.Title("Application"));
        for (var steps = 0; steps < 10 && !startup.IsKeyboardFocused; steps++)
        {
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        Assert.True(startup.IsKeyboardFocused);
        SimulatedKey.Chord(startup, Key.Enter);
        Assert.Equal(["Startup"], screen.OpenSections);

        screen.Theme.Switch(isDark: true);
        Assert.All(ConfigureScreen.SectionHeadings, heading => Assert.Equal(HeadingColor(true), TitleColor(screen, heading)));
        screen.Theme.Switch(isDark: false);
        Assert.All(ConfigureScreen.SectionHeadings, heading => Assert.Equal(HeadingColor(false), TitleColor(screen, heading)));

        screen.ClickTitle("Application");
        Assert.Equal(["Application"], screen.OpenSections);
        Assert.Contains("Current version: 2.1.0", screen.CardTexts);
        Assert.Equal(size, screen.WindowSize);
    }

    [WpfFact]
    public void The_Update_card_follows_an_update_from_available_to_updating_to_failed_and_Auto_update_hides_Update_Now()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        OpenWindows.UseTheme(screen.Window, dark: false);
        var size = screen.WindowSize;
        Assert.Equal(new[] { "Current version: 2.1.0" }, screen.CardTexts.Take(1));
        Assert.False(screen.CardShowsButton("What is new"));
        Assert.False(screen.CardShowsButton("Update Now"));

        screen.PublishUpdate(UpdateStage.Available, "2.2.0");

        Assert.Equal("2.2.0", ConfigureScreen.BoldPart(screen.CardBlock("Available new version")));
        Assert.True(screen.CardShowsButton("What is new"));
        Assert.True(screen.CardShowsButton("Update Now"));
        Assert.Equal(((SolidColorBrush)screen.Window.FindResource("SystemFillColorCautionBackgroundBrush")).Color, screen.CardBackground);

        OpenWindows.Click(screen.CardButton("What is new")!);
        Assert.Same(screen.Window, Assert.Single(screen.Updates.WhatIsNewOwners));
        OpenWindows.Click(screen.CardButton("Update Now")!);
        Assert.Equal(1, screen.Updates.UpdateNowCount);

        screen.PublishUpdate(UpdateStage.Updating, "2.2.0");
        Assert.False(screen.CardButton("Update Now")!.IsEnabled);
        Assert.False(screen.AutoUpdateBox.IsEnabled);
        Assert.Contains("Updating to: 2.2.0", screen.CardTexts);

        screen.PublishUpdate(UpdateStage.NoUpdate, null, "The update could not be downloaded.");
        Assert.Contains("The update could not be downloaded.", screen.CardTexts);
        Assert.False(screen.CardShowsButton("Update Now"));

        screen.PublishUpdate(UpdateStage.Available, "2.3.0");
        screen.SwitchAutoUpdate();

        Assert.Equal([true], screen.Updates.SetAutoUpdateCalls);
        Assert.True(screen.CardShowsButton("What is new"));
        Assert.False(screen.CardShowsButton("Update Now"));
        Assert.Equal(size, screen.WindowSize);
        screen.Log.Events.AssertNoProblems();
    }

    private static Color HeadingColor(bool dark) =>
        (Color)ColorConverter.ConvertFromString(TextLooks.Of(dark).HeadingColor(1)!);

    // The color of the text block that draws the section title's heading.
    private static Color TitleColor(ConfigureScreen screen, string heading) =>
        ((SolidColorBrush)VisualTextBlocks(screen.Title(heading)).First().Foreground).Color;

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
}
