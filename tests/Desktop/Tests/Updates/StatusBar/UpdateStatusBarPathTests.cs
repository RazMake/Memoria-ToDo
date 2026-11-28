using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests.StatusBar;

/// <summary>
/// The updates status bar of Add and Task View on screen, in the paths a user sees: an update found and What is new opened from the bar,
/// an update under way, the keyboard, and a bar that holds its window's start element. The bar's hand pointer comes with it being a
/// button, as for every button of the app, which the platform's own tests check. The bar's sizes and where its colors come from are
/// checked without a window in <see cref="UpdateStatusBarLookTests"/>.
/// </summary>
[UX("StatusBar")]
[Collection(ScreenTests.Collection)]
public class UpdateStatusBarPathTests
{
    [WpfFact]
    public void An_update_is_found_and_What_is_new_opens_from_the_bar_without_starting_it()
    {
        using var screen = new StatusBarScreen(dark: true);
        Assert.Equal(Visibility.Collapsed, screen.Bar.Visibility);

        screen.Publish(UpdateStage.Available, "2.2.0");
        Assert.True(screen.Bar.IsVisible);
        Assert.Equal("Update available: 2.2.0", OpenWindows.TextOf(screen.UpdateText));
        Assert.Equal("Update available", screen.BoldText);
        Assert.Equal(screen.ThemeColor("SystemFillColorCautionBrush"), screen.TextColor);

        OpenWindows.Click(screen.UpdateButton);
        Assert.Same(screen.Window, Assert.Single(screen.Status.WhatIsNewOwners));
        Assert.Equal(0, screen.Status.UpdateNowCount);
    }

    [WpfFact]
    public void An_update_goes_under_way_and_the_bar_follows_the_state_until_it_goes()
    {
        using var screen = new StatusBarScreen(dark: true);
        screen.Publish(UpdateStage.NotInstalled, null);
        Assert.Equal(Visibility.Collapsed, screen.Bar.Visibility);

        screen.Publish(UpdateStage.Updating, "2.2.0");
        Assert.True(screen.Bar.IsVisible);
        Assert.Equal("Updating to 2.2.0", OpenWindows.TextOf(screen.UpdateText));
        Assert.Equal("2.2.0", screen.BoldText);
        Assert.Empty(screen.Buttons);
        Assert.Equal(screen.ThemeColor("TextFillColorSecondaryBrush"), screen.TextColor);

        screen.Publish(UpdateStage.Available, "2.3.0");
        Assert.Equal("Update available: 2.3.0", OpenWindows.TextOf(screen.UpdateText));

        screen.Publish(UpdateStage.NoUpdate, null);
        Assert.Equal(Visibility.Collapsed, screen.Bar.Visibility);
    }

    [WpfFact]
    public void Enter_then_Space_on_the_focused_available_update_open_What_is_new_without_starting_it()
    {
        using var screen = new StatusBarScreen();
        screen.Publish(UpdateStage.Available, "2.2.0");
        screen.Focus(screen.UpdateButton);

        SimulatedKey.Raise(screen.UpdateButton, Key.Enter, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);
        Assert.Same(screen.Window, Assert.Single(screen.Status.WhatIsNewOwners));

        SimulatedKey.Chord(screen.UpdateButton, Key.Space);
        Assert.Equal(2, screen.Status.WhatIsNewOwners.Count);
        Assert.All(screen.Status.WhatIsNewOwners, owner => Assert.Same(screen.Window, owner));
        Assert.Equal(0, screen.Status.UpdateNowCount);
    }

    [WpfFact]
    public void A_bar_with_a_start_element_always_shows_it_and_puts_the_update_text_at_its_end()
    {
        var sync = new Button { Content = "Sync", HorizontalAlignment = HorizontalAlignment.Left };
        var version = new TextBlock { Text = "Version 2.1.0", HorizontalAlignment = HorizontalAlignment.Right };
        using var screen = new StatusBarScreen(start: new Grid { Children = { sync, version } }, dark: true);

        Assert.True(screen.Bar.IsVisible);
        Assert.True(sync.IsVisible);
        Assert.DoesNotContain(screen.Texts, text => text.StartsWith("Update", StringComparison.Ordinal));
        Assert.True(screen.RightOf(version) >= screen.RightOf(screen.Bar) - 16, "The start element doesn't fill the bar, so the version isn't at its right edge.");

        var bar = ((SolidColorBrush)screen.Bar.Background).Color;
        Assert.Equal(screen.ThemeColor("CardBackgroundFillColorDefaultBrush"), bar);
        Assert.NotEqual(((SolidColorBrush)screen.Window.Background).Color, bar);

        screen.Publish(UpdateStage.Available, "2.2.0");
        Assert.Equal("Update available: 2.2.0", OpenWindows.TextOf(screen.UpdateText));
        Assert.True(screen.RightOf(screen.UpdateButton) >= screen.RightOf(screen.Bar) - 16, "The update text isn't at the bar's end.");
        Assert.InRange(screen.LeftOf(screen.UpdateText) - screen.RightOf(version), 0, 16);

        screen.Publish(UpdateStage.Updating, "2.2.0");
        Assert.Equal("Updating to 2.2.0", OpenWindows.TextOf(screen.UpdateText));
        Assert.True(sync.IsVisible);

        screen.Publish(UpdateStage.NoUpdate, null);
        Assert.True(screen.Bar.IsVisible);
        Assert.True(sync.IsVisible);
        Assert.DoesNotContain(screen.Texts, text => text.StartsWith("Update", StringComparison.Ordinal));
    }
}
