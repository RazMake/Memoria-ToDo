using System.Windows;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The update card above Configure's sections, as a user sees it in each state of the update: its box, its lines, and its
/// buttons. What each control does is Updates' business; these tests check that the card shows the state and calls the
/// update status.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureUpdateCardTests
{
    private const string CardEdgeKey = "CardStrokeColorDefaultBrush";
    private const string CautionBackgroundKey = "SystemFillColorCautionBackgroundBrush";
    private const string ErrorKey = "SystemFillColorCriticalBrush";

    [WpfFact]
    public void The_card_sits_above_the_three_sections()
    {
        using var screen = Open(UpdateStage.NoUpdate);

        foreach (var heading in ConfigureScreen.SectionHeadings)
        {
            Assert.True(screen.CardBottom <= screen.TitleTop(heading) + 0.5, $"The card isn't above {heading}.");
        }
    }

    [WpfTheory]
    [InlineData(UpdateStage.NotInstalled)]
    [InlineData(UpdateStage.NoUpdate)]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.Updating)]
    public void The_card_has_a_one_pixel_edge_in_the_card_edge_color_in_every_state(UpdateStage stage)
    {
        using var screen = Open(stage);

        var card = screen.Card;

        Assert.Equal(new Thickness(1), card.BorderThickness);
        Assert.Equal(screen.ThemeColor(CardEdgeKey), ((SolidColorBrush)card.BorderBrush).Color);
    }

    [WpfFact]
    public void The_card_has_the_caution_background_while_a_version_is_available()
    {
        using var screen = Open(UpdateStage.Available);

        Assert.Equal(screen.ThemeColor(CautionBackgroundKey), screen.CardBackground);
    }

    [WpfTheory]
    [InlineData(UpdateStage.NotInstalled)]
    [InlineData(UpdateStage.NoUpdate)]
    [InlineData(UpdateStage.Updating)]
    public void The_card_has_no_background_unless_a_version_is_available(UpdateStage stage)
    {
        using var screen = Open(stage);

        Assert.Null(screen.CardBackground);
    }

    [WpfTheory]
    [InlineData(UpdateStage.NotInstalled)]
    [InlineData(UpdateStage.NoUpdate)]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.Updating)]
    public void Shows_the_current_version_in_every_state(UpdateStage stage)
    {
        using var screen = Open(stage);

        Assert.Equal("Current version: 2.1.0", screen.CardTexts[0]);
    }

    [WpfFact]
    public void With_no_update_shows_only_the_version_and_Auto_update()
    {
        using var screen = Open(UpdateStage.NoUpdate);

        Assert.DoesNotContain(screen.CardTexts, text => text.Contains("version:", StringComparison.Ordinal) && !text.StartsWith("Current", StringComparison.Ordinal));
        Assert.DoesNotContain(screen.CardTexts, text => text.StartsWith("Updating", StringComparison.Ordinal));
        Assert.False(screen.CardShowsButton("What is new"));
        Assert.False(screen.CardShowsButton("Update Now"));
        Assert.True(screen.AutoUpdateBox.IsEnabled);
    }

    [WpfFact]
    public void Shows_the_Auto_update_check_box_with_its_help_line()
    {
        using var screen = Open(UpdateStage.NoUpdate);

        Assert.Contains("Checks for a new version each night around 3 AM, and installs it.", screen.CardTexts);
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_Auto_update_box_shows_whether_Auto_update_is_on(bool on)
    {
        using var screen = Open(UpdateStage.NoUpdate, autoUpdate: on);

        Assert.Equal(on, screen.AutoUpdateBox.IsChecked);
    }

    [WpfFact]
    public void While_a_version_is_available_shows_it_with_the_version_in_bold()
    {
        using var screen = Open(UpdateStage.Available);

        var line = screen.CardBlock("Available new version");

        Assert.Equal("Available new version: 2.2.0", OpenWindows.TextOf(line));
        Assert.Equal("2.2.0", ConfigureScreen.BoldPart(line));
    }

    [WpfFact]
    public void While_a_version_is_available_offers_What_is_new_Update_Now_and_Auto_update()
    {
        using var screen = Open(UpdateStage.Available);

        Assert.True(screen.CardShowsButton("What is new"));
        Assert.True(screen.CardShowsButton("Update Now"));
        Assert.True(screen.CardButton("What is new")!.IsEnabled);
        Assert.True(screen.CardButton("Update Now")!.IsEnabled);
        Assert.True(screen.AutoUpdateBox.IsEnabled);
    }

    [WpfFact]
    public void While_updating_shows_the_new_version_with_the_version_in_bold_and_no_available_line()
    {
        using var screen = Open(UpdateStage.Updating);

        var line = screen.CardBlock("Updating to");

        Assert.Equal("Updating to: 2.2.0", OpenWindows.TextOf(line));
        Assert.Equal("2.2.0", ConfigureScreen.BoldPart(line));
        Assert.DoesNotContain(screen.CardTexts, text => text.StartsWith("Available new version", StringComparison.Ordinal));
    }

    [WpfFact]
    public void While_updating_turns_off_Update_Now_and_Auto_update_but_not_What_is_new()
    {
        using var screen = Open(UpdateStage.Updating);

        Assert.True(screen.CardShowsButton("Update Now"));
        Assert.False(screen.CardButton("Update Now")!.IsEnabled);
        Assert.False(screen.AutoUpdateBox.IsEnabled);
        Assert.True(screen.CardShowsButton("What is new"));
        Assert.True(screen.CardButton("What is new")!.IsEnabled);
    }

    [WpfFact]
    public void In_a_copy_that_is_not_installed_says_updates_work_only_in_an_installed_copy_and_turns_Auto_update_off()
    {
        using var screen = Open(UpdateStage.NotInstalled, autoUpdate: true);

        Assert.Contains("Updates work only in an installed copy.", screen.CardTexts);
        Assert.False(screen.AutoUpdateBox.IsEnabled);
        Assert.False(screen.CardShowsButton("What is new"));
        Assert.False(screen.CardShowsButton("Update Now"));
    }

    [WpfFact]
    public void Shows_the_installed_copy_line_only_in_a_copy_that_is_not_installed()
    {
        using var screen = Open(UpdateStage.NoUpdate);

        Assert.DoesNotContain("Updates work only in an installed copy.", screen.CardTexts);
    }

    [WpfFact]
    public void While_Auto_update_is_on_hides_Update_Now_but_not_What_is_new()
    {
        using var screen = Open(UpdateStage.Available, autoUpdate: true);

        Assert.False(screen.CardShowsButton("Update Now"));
        Assert.True(screen.CardShowsButton("What is new"));
    }

    [WpfFact]
    public void Update_Now_starts_the_update()
    {
        using var screen = Open(UpdateStage.Available);

        screen.Click("Update Now");

        Assert.Equal(1, screen.Updates.UpdateNowCount);
    }

    [WpfTheory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.Updating)]
    public void What_is_new_opens_the_notes_owned_by_the_Configure_window(UpdateStage stage)
    {
        using var screen = Open(stage);

        screen.Click("What is new");

        Assert.Same(screen.Window, Assert.Single(screen.Updates.WhatIsNewOwners));
    }

    [WpfFact]
    public void Shows_a_failure_in_the_error_color()
    {
        using var screen = Open(UpdateStage.Available, failure: "The update to 2.2.0 failed: no disk space.");

        var line = screen.CardBlock("The update to 2.2.0 failed");

        Assert.Equal("The update to 2.2.0 failed: no disk space.", OpenWindows.TextOf(line));
        Assert.Equal(screen.ThemeColor(ErrorKey), ConfigureScreen.ColorOf(line));
    }

    [WpfFact]
    public void Shows_no_failure_line_when_nothing_failed()
    {
        using var screen = Open(UpdateStage.Available);

        Assert.DoesNotContain(screen.CardTexts, text => text.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }

    [WpfFact]
    public void Follows_the_state_as_it_changes_while_the_window_is_open()
    {
        using var screen = Open(UpdateStage.NoUpdate);

        screen.PublishUpdate(UpdateStage.Available, "2.2.0");
        Assert.Contains("Available new version: 2.2.0", screen.CardTexts);
        Assert.True(screen.CardShowsButton("Update Now"));
        Assert.Equal(screen.ThemeColor(CautionBackgroundKey), screen.CardBackground);

        screen.PublishUpdate(UpdateStage.Updating, "2.2.0");
        Assert.Contains("Updating to: 2.2.0", screen.CardTexts);
        Assert.DoesNotContain("Available new version: 2.2.0", screen.CardTexts);
        Assert.False(screen.CardButton("Update Now")!.IsEnabled);
        Assert.Null(screen.CardBackground);

        screen.PublishUpdate(UpdateStage.NoUpdate, null);
        Assert.False(screen.CardShowsButton("Update Now"));
        Assert.DoesNotContain(screen.CardTexts, text => text.StartsWith("Updating", StringComparison.Ordinal));
        Assert.Equal("Current version: 2.1.0", screen.CardTexts[0]);
    }

    [WpfFact]
    public void Follows_Auto_update_as_it_changes_while_the_window_is_open()
    {
        using var screen = Open(UpdateStage.Available);

        screen.Updates.AutoUpdate = true;
        screen.Updates.RaiseChanged();

        Assert.True(screen.AutoUpdateBox.IsChecked);
        Assert.False(screen.CardShowsButton("Update Now"));
    }

    [WpfFact]
    public void The_window_keeps_its_size_when_an_update_appears()
    {
        using var screen = Open(UpdateStage.NoUpdate);
        var before = screen.WindowSize;

        screen.PublishUpdate(UpdateStage.Available, "2.2.0");

        Assert.Equal(before, screen.WindowSize);
    }

    [WpfFact]
    public void Switching_the_Auto_update_box_on_calls_SetAutoUpdate_true_at_once()
    {
        using var screen = Open(UpdateStage.NoUpdate);

        screen.SwitchAutoUpdate();

        Assert.Equal([true], screen.Updates.SetAutoUpdateCalls);
        Assert.True(screen.AutoUpdateBox.IsChecked);
    }

    [WpfFact]
    public void Switching_the_Auto_update_box_off_calls_SetAutoUpdate_false()
    {
        using var screen = Open(UpdateStage.NoUpdate, autoUpdate: true);

        screen.SwitchAutoUpdate();

        Assert.Equal([false], screen.Updates.SetAutoUpdateCalls);
        Assert.False(screen.AutoUpdateBox.IsChecked);
    }

    [WpfFact]
    public void A_failed_switch_puts_the_box_back_and_shows_why_in_the_error_color()
    {
        using var screen = Open(UpdateStage.NoUpdate);
        screen.Updates.SetAutoUpdateFailure = new InvalidOperationException("The setting file is read-only.");

        screen.SwitchAutoUpdate();

        Assert.False(screen.AutoUpdateBox.IsChecked);
        Assert.Equal([true], screen.Updates.SetAutoUpdateCalls);
        var line = screen.CardBlock("The setting file is read-only.");
        Assert.Equal(screen.ThemeColor(ErrorKey), ConfigureScreen.ColorOf(line));
    }

    [WpfFact]
    public void A_failed_switch_off_keeps_the_box_on_and_shows_why()
    {
        using var screen = Open(UpdateStage.NoUpdate, autoUpdate: true);
        screen.Updates.SetAutoUpdateFailure = new InvalidOperationException("The setting file is read-only.");

        screen.SwitchAutoUpdate();

        Assert.True(screen.AutoUpdateBox.IsChecked);
        Assert.Equal([false], screen.Updates.SetAutoUpdateCalls);
        Assert.Contains("The setting file is read-only.", screen.CardTexts);
    }

    private static ConfigureScreen Open(UpdateStage stage, bool autoUpdate = false, string? failure = null)
    {
        var screen = new ConfigureScreen();
        var newVersion = stage is UpdateStage.Available or UpdateStage.Updating ? "2.2.0" : null;
        screen.Updates.State = new UpdateState(stage, "2.1.0", newVersion, failure);
        screen.Updates.AutoUpdate = autoUpdate;
        screen.Open();
        OpenWindows.UseTheme(screen.Window, dark: false);
        return screen;
    }
}
