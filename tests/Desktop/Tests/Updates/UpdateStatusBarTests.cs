using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>
/// The status bar of Add and Task View on screen, in its common scenarios: hidden or shown by the state, the update
/// text's look, and opening What is new from it, never starting the update. The bar's hand pointer comes with it being a button, as for every button of the app,
/// which the platform's own tests check.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class UpdateStatusBarTests
{
    [WpfFact]
    public void Is_hidden_when_there_is_no_update_and_no_start_element()
    {
        using var screen = new StatusBarScreen();

        Assert.Equal(Visibility.Collapsed, screen.Bar.Visibility);
    }

    [WpfFact]
    public void Is_hidden_when_the_app_is_not_installed()
    {
        using var screen = new StatusBarScreen();

        screen.Publish(UpdateStage.NotInstalled, null);

        Assert.Equal(Visibility.Collapsed, screen.Bar.Visibility);
    }

    [WpfFact]
    public void Always_shows_with_a_start_element_and_has_no_update_text_without_an_update()
    {
        var sync = new Button { Content = "Sync" };
        using var screen = new StatusBarScreen(start: sync);

        Assert.True(screen.Bar.IsVisible);
        Assert.True(sync.IsVisible);
        Assert.DoesNotContain(screen.Texts, text => text.StartsWith("Update", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Says_update_available_with_the_version_and_only_the_words_in_bold()
    {
        using var screen = new StatusBarScreen();

        screen.Publish(UpdateStage.Available, "2.2.0");

        Assert.True(screen.Bar.IsVisible);
        Assert.Equal("Update available: 2.2.0", OpenWindows.TextOf(screen.UpdateText));
        Assert.Equal("Update available", screen.BoldText);
    }

    [WpfFact]
    public void Shows_an_available_update_in_the_caution_color_of_the_theme()
    {
        using var screen = new StatusBarScreen(dark: true);

        screen.Publish(UpdateStage.Available, "2.2.0");

        Assert.Equal(screen.ThemeColor("SystemFillColorCautionBrush"), screen.TextColor);
    }

    [WpfFact]
    public void A_click_on_the_available_update_opens_What_is_new_over_the_bars_window_and_does_not_start_it()
    {
        using var screen = new StatusBarScreen();
        screen.Publish(UpdateStage.Available, "2.2.0");

        OpenWindows.Click(screen.UpdateButton);

        Assert.Same(screen.Window, Assert.Single(screen.Status.WhatIsNewOwners));
        Assert.Equal(0, screen.Status.UpdateNowCount);
    }

    [WpfFact]
    public void Enter_on_the_focused_available_update_opens_What_is_new_and_does_not_start_it()
    {
        using var screen = new StatusBarScreen();
        screen.Publish(UpdateStage.Available, "2.2.0");
        screen.Focus(screen.UpdateButton);

        SimulatedKey.Raise(screen.UpdateButton, Key.Enter, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

        Assert.Same(screen.Window, Assert.Single(screen.Status.WhatIsNewOwners));
        Assert.Equal(0, screen.Status.UpdateNowCount);
    }

    [WpfFact]
    public void Space_on_the_focused_available_update_opens_What_is_new_and_does_not_start_it()
    {
        using var screen = new StatusBarScreen();
        screen.Publish(UpdateStage.Available, "2.2.0");
        screen.Focus(screen.UpdateButton);

        SimulatedKey.Chord(screen.UpdateButton, Key.Space);

        Assert.Same(screen.Window, Assert.Single(screen.Status.WhatIsNewOwners));
        Assert.Equal(0, screen.Status.UpdateNowCount);
    }

    [WpfFact]
    public void Says_updating_to_the_version_with_the_version_in_bold_and_is_no_button()
    {
        using var screen = new StatusBarScreen();

        screen.Publish(UpdateStage.Updating, "2.2.0");

        Assert.True(screen.Bar.IsVisible);
        Assert.Equal("Updating to 2.2.0", OpenWindows.TextOf(screen.UpdateText));
        Assert.Equal("2.2.0", screen.BoldText);
        Assert.Empty(screen.Buttons);
    }

    [WpfFact]
    public void Shows_an_update_under_way_in_the_secondary_text_color_of_the_theme()
    {
        using var screen = new StatusBarScreen(dark: true);

        screen.Publish(UpdateStage.Updating, "2.2.0");

        Assert.Equal(screen.ThemeColor("TextFillColorSecondaryBrush"), screen.TextColor);
    }

    [WpfFact]
    public void Follows_the_state_while_the_window_is_open()
    {
        using var screen = new StatusBarScreen();

        screen.Publish(UpdateStage.Available, "2.2.0");
        Assert.Equal("Update available: 2.2.0", OpenWindows.TextOf(screen.UpdateText));

        screen.Publish(UpdateStage.Updating, "2.2.0");
        Assert.Equal("Updating to 2.2.0", OpenWindows.TextOf(screen.UpdateText));

        screen.Publish(UpdateStage.Available, "2.3.0");
        Assert.Equal("Update available: 2.3.0", OpenWindows.TextOf(screen.UpdateText));

        screen.Publish(UpdateStage.NoUpdate, null);
        Assert.Equal(Visibility.Collapsed, screen.Bar.Visibility);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Has_a_background_of_the_theme_that_differs_from_the_windows(bool dark)
    {
        using var screen = new StatusBarScreen(start: new Button { Content = "Sync" }, dark: dark);

        var bar = ((SolidColorBrush)screen.Bar.Background).Color;
        Assert.Equal(screen.ThemeColor("CardBackgroundFillColorDefaultBrush"), bar);
        Assert.NotEqual(((SolidColorBrush)screen.Window.Background).Color, bar);
    }

    [WpfFact]
    public void Keeps_the_start_element_when_the_update_text_goes()
    {
        var sync = new Button { Content = "Sync" };
        using var screen = new StatusBarScreen(start: sync);
        screen.Publish(UpdateStage.Available, "2.2.0");

        screen.Publish(UpdateStage.NoUpdate, null);

        Assert.True(screen.Bar.IsVisible);
        Assert.True(sync.IsVisible);
        Assert.DoesNotContain(screen.Texts, text => text.StartsWith("Update", StringComparison.Ordinal));
    }
}
