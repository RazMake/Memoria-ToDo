using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Update card of Configure's Application section, built in memory with no window: each stage the update status can
/// be in, what its buttons, including Check for updates, and the Auto update box do, and what a failed switch or check shows and logs.
/// </summary>
[Unit]
public class UpdateCardTests
{
    private readonly FakeUpdateStatus _updates = new();
    private readonly InMemoryLogSink _log = new();
    private readonly SolidColorBrush _errorBrush = new(Colors.Red);

    private UpdateCard Card()
    {
        var card = new UpdateCard(_updates, _log.CreateLogger());
        card.Resources["SystemFillColorCriticalBrush"] = _errorBrush;
        return card;
    }

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

    private const string Checking = "Checking for updates…";
    private const string Latest = "You have the latest version.";
    private const string CheckButton = "Check for updates";

    private static void Load(UpdateCard card) => card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

    // Lets the card's continuation after a check, which the dispatcher queues, run.
    private static void Flush(UpdateCard card) => card.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);

    private static TextBlock Block(UpdateCard card, string text) =>
        OpenWindows.Descendants(card).OfType<TextBlock>()
            .Single(block => block.Visibility == Visibility.Visible && OpenWindows.TextOf(block) == text);

    private static void Arrange(UpdateCard card)
    {
        card.Measure(new Size(700, double.PositiveInfinity));
        card.Arrange(new Rect(card.DesiredSize));
    }

    private static double Top(UpdateCard card, UIElement element) => element.TranslatePoint(new Point(0, 0), card).Y;

    private static double Bottom(UpdateCard card, FrameworkElement element) => Top(card, element) + element.ActualHeight;

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
    public void Auto_update_on_still_shows_Update_Now_while_an_update_is_available()
    {
        _updates.State = State(UpdateStage.Available, "2.2.0");
        _updates.AutoUpdate = true;

        var card = Card();

        Assert.True(Shows(card, "What is new"));
        Assert.True(Shows(card, "Update Now"));
        Assert.True(ButtonOf(card, "Update Now").IsEnabled);
        Assert.True(AutoUpdate(card).IsChecked);
    }

    [WpfFact]
    public void Update_Now_shows_with_Auto_update_off_or_on_only_while_a_version_is_available_or_updating()
    {
        _updates.AutoUpdate = true;
        _updates.State = State(UpdateStage.NoUpdate);

        var card = Card();
        card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.False(Shows(card, "Update Now"));

        _updates.Publish(State(UpdateStage.Updating, "2.2.0"));

        Assert.True(Shows(card, "Update Now"));
        Assert.False(ButtonOf(card, "Update Now").IsEnabled);
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

    [WpfFact]
    public void The_Check_for_updates_button_has_the_hand_pointer_and_sits_on_its_own_line_after_What_is_new_and_Update_Now()
    {
        _updates.State = State(UpdateStage.Available, "2.2.0");
        var card = Card();

        Arrange(card);

        var check = ButtonOf(card, CheckButton);
        Assert.Equal(Cursors.Hand, check.Cursor);
        Assert.True(Top(card, check) >= Bottom(card, ButtonOf(card, "What is new")));
        Assert.True(Top(card, check) >= Bottom(card, ButtonOf(card, "Update Now")));
    }

    [WpfFact]
    public void With_no_update_the_Check_for_updates_button_follows_the_version_line()
    {
        var card = Card();

        Arrange(card);

        Assert.True(Shows(card, CheckButton));
        Assert.True(Top(card, ButtonOf(card, CheckButton)) >= Bottom(card, Block(card, "Current version: 2.1.0")));
    }

    [WpfFact]
    public void Clicking_Check_for_updates_checks_now_and_while_it_runs_turns_the_button_off_and_says_so()
    {
        var hold = new TaskCompletionSource<string?>();
        _updates.CheckNowHold = hold;
        var card = Card();

        Click(ButtonOf(card, CheckButton));

        Assert.Equal(1, _updates.CheckNowCount);
        Assert.False(ButtonOf(card, CheckButton).IsEnabled);
        Assert.Contains(Checking, Texts(card));

        hold.SetResult(null);
        Flush(card);

        Assert.True(ButtonOf(card, CheckButton).IsEnabled);
        Assert.DoesNotContain(Checking, Texts(card));
    }

    [WpfFact]
    public void A_check_that_finds_nothing_says_you_have_the_latest_version_until_the_next_check_starts()
    {
        var card = Card();

        Click(ButtonOf(card, CheckButton));

        Assert.Contains(Latest, Texts(card));
        Assert.True(ButtonOf(card, CheckButton).IsEnabled);

        _updates.CheckNowHold = new TaskCompletionSource<string?>();
        Click(ButtonOf(card, CheckButton));

        Assert.DoesNotContain(Latest, Texts(card));
        Assert.Contains(Checking, Texts(card));
    }

    [WpfFact]
    public void A_check_that_finds_a_newer_version_turns_the_card_to_Available_and_says_nothing_more()
    {
        var hold = new TaskCompletionSource<string?>();
        _updates.CheckNowHold = hold;
        var card = Card();
        Load(card);
        Click(ButtonOf(card, CheckButton));

        _updates.Publish(State(UpdateStage.Available, "2.2.0"));
        hold.SetResult(null);
        Flush(card);

        Assert.True(card.Cautioned);
        Assert.Contains("Available new version: 2.2.0", Texts(card));
        Assert.DoesNotContain(Checking, Texts(card));
        Assert.DoesNotContain(Latest, Texts(card));
    }

    [WpfFact]
    public void A_failed_check_shows_the_reason_in_the_error_color_keeps_the_state_and_logs_nothing()
    {
        _updates.State = State(UpdateStage.Available, "2.2.0");
        _updates.CheckNowResult = "No network.";
        var card = Card();

        Click(ButtonOf(card, CheckButton));

        Assert.Same(_errorBrush, Block(card, "The check for updates failed: No network.").Foreground);
        Assert.DoesNotContain(Checking, Texts(card));
        Assert.DoesNotContain(Latest, Texts(card));
        Assert.True(ButtonOf(card, CheckButton).IsEnabled);
        Assert.True(card.Cautioned);
        Assert.True(Shows(card, "Update Now"));
        Assert.DoesNotContain(_log.Events, entry => entry.Level >= LogEventLevel.Information);
    }

    [WpfFact]
    public void A_check_the_app_stop_cancels_ends_quietly_with_no_escaped_exception_no_failure_text_and_the_button_on_again()
    {
        var hold = new TaskCompletionSource<string?>();
        _updates.CheckNowHold = hold;
        var card = Card();
        var escaped = new List<Exception>();
        void OnUnhandled(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            escaped.Add(e.Exception);
            e.Handled = true;
        }

        card.Dispatcher.UnhandledException += OnUnhandled;
        try
        {
            Click(ButtonOf(card, CheckButton));

            hold.SetCanceled();
            Flush(card);
        }
        finally
        {
            card.Dispatcher.UnhandledException -= OnUnhandled;
        }

        Assert.Empty(escaped);
        Assert.DoesNotContain(Texts(card), text => text.StartsWith("The check for updates failed", StringComparison.Ordinal));
        Assert.True(ButtonOf(card, CheckButton).IsEnabled);
        Assert.DoesNotContain(Checking, Texts(card));
    }

    [WpfFact]
    public void The_next_check_that_succeeds_clears_the_failure()
    {
        _updates.CheckNowResult = "No network.";
        var card = Card();
        Click(ButtonOf(card, CheckButton));
        _updates.CheckNowResult = null;

        Click(ButtonOf(card, CheckButton));

        Assert.DoesNotContain(Texts(card), text => text.StartsWith("The check for updates failed", StringComparison.Ordinal));
        Assert.Contains(Latest, Texts(card));
    }

    [WpfFact]
    public void The_Check_for_updates_button_is_turned_off_while_updating_and_on_again_after()
    {
        _updates.State = State(UpdateStage.Updating, "2.2.0");
        var card = Card();
        Load(card);

        Assert.False(ButtonOf(card, CheckButton).IsEnabled);

        _updates.Publish(State(UpdateStage.NoUpdate));

        Assert.True(ButtonOf(card, CheckButton).IsEnabled);
    }

    [WpfFact]
    public void A_copy_that_is_not_installed_has_no_Check_for_updates_button()
    {
        _updates.State = State(UpdateStage.NotInstalled);

        var card = Card();

        Assert.DoesNotContain(
            OpenWindows.Descendants(card).OfType<Button>(),
            button => button.Content as string == CheckButton && button.Visibility == Visibility.Visible);
    }
}
