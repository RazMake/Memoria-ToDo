using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Task View's status bar runs along the window's bottom edge and always shows. The Sync button and its failure line
/// sit at its start; the update text, while a version is available or updating, sits at its end.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListStatusBarTests
{
    [WpfFact]
    public void The_status_bar_shows_with_the_Sync_button_while_there_is_no_update()
    {
        using var screen = new TaskListScreen(new ListServices());

        Assert.True(screen.StatusBarShows);
        Assert.True(screen.IsInStatusBar(screen.SyncButton));
        Assert.DoesNotContain(screen.StatusBarTexts, text => text.StartsWith("Update", StringComparison.Ordinal));
    }

    [WpfFact]
    public void The_status_bar_shows_with_the_Sync_button_in_a_copy_that_is_not_installed()
    {
        var services = new ListServices();
        services.Updates.State = new UpdateState(UpdateStage.NotInstalled, "2.1.0", null, null);
        using var screen = new TaskListScreen(services);

        Assert.True(screen.StatusBarShows);
        Assert.True(screen.IsInStatusBar(screen.SyncButton));
    }

    [WpfFact]
    public void The_status_bar_runs_along_the_bottom_edge_below_the_task_list_and_details()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        var bar = screen.StatusBar;

        Assert.InRange(screen.BottomOf(bar), screen.ContentBottom - 1, screen.ContentBottom + 1);
        Assert.True(screen.TopOf(bar) >= screen.BottomOf(screen.Details) - 0.5, "The bar isn't below the details.");
        Assert.True(screen.LeftOf(bar) <= screen.LeftOf(screen.Details) && screen.RightOf(bar) >= screen.RightOf(screen.Details), "The bar doesn't span the window.");
    }

    [WpfFact]
    public void The_Sync_button_sits_at_the_start_of_the_bar_not_above_the_lists()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        var sync = screen.SyncButton;

        Assert.True(screen.TopOf(sync) >= screen.BottomOf(screen.Details) - 0.5, "The Sync button isn't below the details.");
        Assert.True(screen.LeftOf(sync) - screen.LeftOf(screen.StatusBar) < 24, "The Sync button isn't at the bar's start.");
    }

    [WpfFact]
    public void The_Sync_button_is_compact_with_small_padding_the_sync_icon_and_its_tooltip()
    {
        using var screen = new TaskListScreen(new ListServices());
        screen.UseTheme();

        var sync = screen.SyncButton;

        Assert.True(sync.Padding.Left <= 8 && sync.Padding.Right <= 8, $"Too much padding at the sides: {sync.Padding}.");
        Assert.True(sync.Padding.Top <= 3 && sync.Padding.Bottom <= 3, $"Too much padding above and below: {sync.Padding}.");
        Assert.Equal("Sync (F5)", sync.ToolTip is ToolTip { Content: var tip } ? tip?.ToString() : sync.ToolTip?.ToString());
        Assert.DoesNotContain("Sync", screen.StatusBarTexts);
        Assert.Contains("\uE895", screen.StatusBarTexts);
    }

    [WpfFact]
    public void The_Sync_button_stays_bright_green_while_it_is_off_not_greyed_out()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        screen.UseTheme();

        screen.PublishSync(SyncState.Working);

        Assert.False(screen.SyncButton.IsEnabled);
        var fill = screen.SyncButtonFill;
        var green = screen.ThemeColor("SystemFillColorSuccessBrush");
        Assert.True(Math.Abs(fill.R - green.R) <= 6 && Math.Abs(fill.G - green.G) <= 6 && Math.Abs(fill.B - green.B) <= 6,
            $"The button is {fill}, not the theme's green {green}.");
    }

    [WpfFact]
    public void A_failed_sync_shows_its_details_in_the_bar_after_the_Sync_button()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);
        services.Sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Connection, Details = "Requested sync failed"
        });

        screen.Click("Sync");

        var line = screen.FailureLine("Requested sync failed");
        Assert.NotNull(line);
        Assert.True(screen.IsInStatusBar(line));
        Assert.True(screen.LeftOf(line) >= screen.RightOf(screen.SyncButton) - 0.5, "The failure line isn't after the button.");
    }

    [WpfFact]
    public void Shows_Update_available_with_the_version_at_the_end_of_the_bar_while_a_version_is_available()
    {
        var services = new ListServices();
        services.Updates.State = new UpdateState(UpdateStage.Available, "2.1.0", "2.2.0", null);
        using var screen = new TaskListScreen(services);

        Assert.Contains("Update available: 2.2.0", screen.StatusBarTexts);
        Assert.True(screen.RightOf(screen.UpdateTextButton) >= screen.RightOf(screen.StatusBar) - 16, "The update text isn't at the bar's end.");
        Assert.True(screen.LeftOf(screen.UpdateTextButton) > screen.RightOf(screen.SyncButton), "The update text isn't after the Sync button.");
    }

    [WpfFact]
    public void Shows_Updating_to_the_version_while_updating()
    {
        var services = new ListServices();
        services.Updates.State = new UpdateState(UpdateStage.Updating, "2.1.0", "2.2.0", null);
        using var screen = new TaskListScreen(services);

        Assert.Contains("Updating to 2.2.0", string.Concat(screen.StatusBarTexts));
        Assert.True(screen.IsInStatusBar(screen.SyncButton));
    }

    [WpfFact]
    public void Clicking_the_update_text_starts_the_update()
    {
        var services = new ListServices();
        services.Updates.State = new UpdateState(UpdateStage.Available, "2.1.0", "2.2.0", null);
        using var screen = new TaskListScreen(services);

        OpenWindows.Click((Button)screen.UpdateTextButton);

        Assert.Equal(1, services.Updates.UpdateNowCount);
    }

    [WpfFact]
    public void The_update_text_follows_the_state_while_Task_View_is_open()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);

        screen.PublishUpdate(UpdateStage.Available, "2.2.0");
        Assert.Contains("Update available", string.Concat(screen.StatusBarTexts));

        screen.PublishUpdate(UpdateStage.Updating, "2.2.0");
        Assert.Contains("Updating to 2.2.0", string.Concat(screen.StatusBarTexts));

        screen.PublishUpdate(UpdateStage.NoUpdate, null);
        Assert.DoesNotContain("Update", string.Concat(screen.StatusBarTexts));
        Assert.True(screen.StatusBarShows);
    }
}
