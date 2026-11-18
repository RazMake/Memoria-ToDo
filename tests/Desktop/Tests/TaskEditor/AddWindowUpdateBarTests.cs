using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Add shows Updates' status bar along its bottom edge, below Save, only while a new version is available or updating,
/// and grows by the bar's height. Add shows no update text otherwise, so it looks as it does with no Updates.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowUpdateBarTests
{
    [WpfFact]
    public void Shows_no_status_bar_while_there_is_no_update()
    {
        using var screen = Open();

        Assert.False(screen.ShowsUpdateBar);
        Assert.False(screen.ShowsText("Update"));
    }

    [WpfFact]
    public void Shows_no_status_bar_in_a_copy_that_is_not_installed()
    {
        using var screen = new AddScreen();
        screen.Services.Updates.State = new UpdateState(UpdateStage.NotInstalled, "2.1.0", null, null);
        screen.Show();

        Assert.False(screen.ShowsUpdateBar);
    }

    [WpfFact]
    public void Shows_Update_available_with_the_version_while_a_version_is_available()
    {
        using var screen = Open(UpdateStage.Available, "2.2.0");

        Assert.True(screen.ShowsUpdateBar);
        Assert.Equal("Update available: 2.2.0", string.Concat(screen.UpdateBarTexts));
    }

    [WpfFact]
    public void Shows_Updating_to_the_version_while_updating()
    {
        using var screen = Open(UpdateStage.Updating, "2.2.0");

        Assert.True(screen.ShowsUpdateBar);
        Assert.Equal("Updating to 2.2.0", string.Concat(screen.UpdateBarTexts));
    }

    [WpfFact]
    public void The_status_bar_runs_along_the_bottom_edge_below_Save()
    {
        using var screen = Open(UpdateStage.Available, "2.2.0");

        var (bar, save, contentHeight) = screen.BarPlace;

        Assert.True(bar.Top >= save.Bottom - 0.5, $"The bar starts at {bar.Top}, above Save's bottom {save.Bottom}.");
        Assert.InRange(bar.Bottom, contentHeight - 1, contentHeight + 1);
    }

    [WpfFact]
    public void Clicking_the_update_text_opens_What_is_new_over_Add_and_does_not_start_the_update()
    {
        using var screen = Open(UpdateStage.Available, "2.2.0");

        OpenWindows.Click(screen.UpdateTextButton);

        Assert.Same(screen.Window, Assert.Single(screen.Services.Updates.WhatIsNewOwners));
        Assert.Equal(0, screen.Services.Updates.UpdateNowCount);
    }

    [WpfFact]
    public void The_status_bar_follows_the_state_while_Add_is_open()
    {
        using var screen = Open();

        screen.PublishUpdate(UpdateStage.Available, "2.2.0");
        Assert.True(screen.ShowsUpdateBar);
        Assert.Equal("Update available: 2.2.0", string.Concat(screen.UpdateBarTexts));

        screen.PublishUpdate(UpdateStage.Updating, "2.2.0");
        Assert.Equal("Updating to 2.2.0", string.Concat(screen.UpdateBarTexts));

        screen.PublishUpdate(UpdateStage.NoUpdate, null);
        Assert.False(screen.ShowsUpdateBar);
    }

    [WpfFact]
    public void Add_grows_by_the_height_of_the_bar_while_it_shows_and_shrinks_back_after()
    {
        using var screen = Open();
        var plain = screen.WindowHeight;

        screen.PublishUpdate(UpdateStage.Available, "2.2.0");
        var barHeight = screen.UpdateBar!.ActualHeight;
        Assert.True(barHeight > 0, "The bar has no height.");
        Assert.Equal(plain + barHeight, screen.WindowHeight, 1.0);

        screen.PublishUpdate(UpdateStage.NoUpdate, null);
        Assert.Equal(plain, screen.WindowHeight, 1.0);
    }

    private static AddScreen Open(UpdateStage stage = UpdateStage.NoUpdate, string? newVersion = null)
    {
        var screen = new AddScreen();
        screen.Services.Updates.State = new UpdateState(stage, "2.1.0", newVersion, null);
        screen.Show();
        screen.UseTheme(dark: false);
        return screen;
    }
}
