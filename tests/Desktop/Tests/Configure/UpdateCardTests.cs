using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Update card of Configure's Application section, built in memory with no window: each stage the update status can
/// be in, what its buttons and the Auto update box do, and what a failed switch shows and logs.
/// </summary>
[Unit]
public class UpdateCardTests
{
    private readonly FakeUpdateStatus _updates = new();
    private readonly InMemoryLogSink _log = new();

    private UpdateCard Card() => new(_updates, _log.CreateLogger());

    private static UpdateState State(UpdateStage stage, string? newVersion = null, string? failure = null) =>
        new(stage, "2.1.0", newVersion, failure);

    private static IReadOnlyList<string> Texts(UpdateCard card) =>
        [.. OpenWindows.Descendants(card).OfType<TextBlock>()
            .Where(block => block.Visibility == Visibility.Visible)
            .Select(OpenWindows.TextOf).Where(text => text.Length > 0)];

    private static bool Shows(UpdateCard card, string button) =>
        OpenWindows.Descendants(card).OfType<Button>().Single(b => b.Content as string == button).Visibility == Visibility.Visible;

    private static Button ButtonOf(UpdateCard card, string text) =>
        OpenWindows.Descendants(card).OfType<Button>().Single(b => b.Content as string == text);

    private static CheckBox AutoUpdate(UpdateCard card) =>
        OpenWindows.Descendants(card).OfType<CheckBox>().Single(box => box.Content as string == "Auto update");

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    [WpfFact]
    public void With_no_update_the_card_shows_only_the_current_version_and_the_Auto_update_box()
    {
        var card = Card();

        Assert.Equal("Current version: 2.1.0", Texts(card)[0]);
        Assert.DoesNotContain(Texts(card), text => text.StartsWith("Available new version", StringComparison.Ordinal));
        Assert.False(Shows(card, "What is new"));
        Assert.False(Shows(card, "Update Now"));
        Assert.True(AutoUpdate(card).IsEnabled);
        Assert.False(card.Cautioned);
    }

    [WpfFact]
    public void An_available_update_shows_the_new_version_and_the_What_is_new_and_Update_Now_buttons_and_cautions_the_card()
    {
        _updates.State = State(UpdateStage.Available, "2.2.0");

        var card = Card();

        Assert.Contains("Available new version: 2.2.0", Texts(card));
        Assert.True(Shows(card, "What is new"));
        Assert.True(Shows(card, "Update Now"));
        Assert.True(ButtonOf(card, "Update Now").IsEnabled);
        Assert.True(card.Cautioned);
    }

    [WpfFact]
    public void Auto_update_on_hides_Update_Now_while_an_update_is_available()
    {
        _updates.State = State(UpdateStage.Available, "2.2.0");
        _updates.AutoUpdate = true;

        var card = Card();

        Assert.True(Shows(card, "What is new"));
        Assert.False(Shows(card, "Update Now"));
        Assert.True(AutoUpdate(card).IsChecked);
    }

    [WpfFact]
    public void An_update_in_progress_shows_the_version_being_installed_and_turns_off_Update_Now_and_Auto_update()
    {
        _updates.State = State(UpdateStage.Updating, "2.2.0");

        var card = Card();

        Assert.Contains("Updating to: 2.2.0", Texts(card));
        Assert.False(ButtonOf(card, "Update Now").IsEnabled);
        Assert.False(AutoUpdate(card).IsEnabled);
        Assert.False(card.Cautioned);
    }

    [WpfFact]
    public void A_failed_update_shows_its_message()
    {
        _updates.State = State(UpdateStage.NoUpdate, failure: "The update could not be downloaded.");

        var card = Card();

        Assert.Contains("The update could not be downloaded.", Texts(card));
    }

    [WpfFact]
    public void A_copy_that_is_not_installed_says_updates_work_only_in_an_installed_copy_and_turns_off_Auto_update()
    {
        _updates.State = State(UpdateStage.NotInstalled);
        _updates.AutoUpdate = true;

        var card = Card();

        Assert.Contains("Updates work only in an installed copy.", Texts(card));
        Assert.False(AutoUpdate(card).IsEnabled);
        Assert.False(AutoUpdate(card).IsChecked);
    }

    [WpfFact]
    public void Clicking_Update_Now_asks_the_status_to_update()
    {
        _updates.State = State(UpdateStage.Available, "2.2.0");
        var card = Card();

        Click(ButtonOf(card, "Update Now"));

        Assert.Equal(1, _updates.UpdateNowCount);
    }

    [WpfFact]
    public void Switching_Auto_update_on_and_off_tells_the_status_each_time_and_shows_no_problem()
    {
        var card = Card();

        AutoUpdate(card).IsChecked = true;
        AutoUpdate(card).IsChecked = false;

        Assert.Equal([true, false], _updates.SetAutoUpdateCalls);
        Assert.Contains("Current version: 2.1.0", Texts(card));
        _log.Events.AssertNoProblems();
    }

    [WpfFact]
    public void A_failed_Auto_update_switch_shows_the_error_puts_the_box_back_and_logs_it()
    {
        var failure = new InvalidOperationException("The task could not be changed.");
        _updates.SetAutoUpdateFailure = failure;
        var card = Card();

        AutoUpdate(card).IsChecked = true;

        Assert.Contains("The task could not be changed.", Texts(card));
        Assert.False(AutoUpdate(card).IsChecked);
        Assert.Contains(_log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));
    }

    [WpfFact]
    public void A_refresh_from_the_status_does_not_call_SetAutoUpdate_again()
    {
        var card = Card();

        _updates.AutoUpdate = true;
        _updates.RaiseChanged();
        card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        _updates.Publish(State(UpdateStage.Available, "2.2.0"));

        Assert.Empty(_updates.SetAutoUpdateCalls);
        Assert.True(AutoUpdate(card).IsChecked);
        Assert.True(Shows(card, "What is new"));
    }
}
