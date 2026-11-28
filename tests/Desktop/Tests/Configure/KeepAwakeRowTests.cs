using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.KeepAwake;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Prevent screen saver row of Configure's Behavior section, built in memory with no window: its check box, its help
/// line, the message that shows a failure in the error color, and how it follows Keep awake while loaded.
/// </summary>
[Unit]
public class KeepAwakeRowTests
{
    private const string Help = "Keeps the screen on while the app runs. Locking the PC pauses it until you unlock.";
    private const string ErrorKey = "SystemFillColorCriticalBrush";

    private readonly FakeKeepAwakeStatus _keepAwake = new();
    private readonly InMemoryLogSink _log = new();
    private readonly SolidColorBrush _errorBrush = new(Colors.Red);

    private KeepAwakeRow Row()
    {
        var row = new KeepAwakeRow(_keepAwake, _log.CreateLogger());
        row.Resources[ErrorKey] = _errorBrush;
        return row;
    }

    private static CheckBox Box(KeepAwakeRow row) => OpenWindows.Descendants(row).OfType<CheckBox>().Single();

    private static IReadOnlyList<string> Texts(KeepAwakeRow row) =>
        [.. OpenWindows.Descendants(row).OfType<TextBlock>()
            .Where(block => block.Visibility == Visibility.Visible)
            .Select(OpenWindows.TextOf).Where(text => text.Length > 0)];

    private static TextBlock Block(KeepAwakeRow row, string text) =>
        OpenWindows.Descendants(row).OfType<TextBlock>()
            .Single(block => block.Visibility == Visibility.Visible && OpenWindows.TextOf(block) == text);

    private static void Load(KeepAwakeRow row) => row.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

    [WpfFact]
    public void Shows_the_check_box_named_Prevent_screen_saver_with_its_help_line_under_it()
    {
        var row = Row();

        Assert.Equal("Prevent screen saver", Box(row).Content);
        Assert.Equal([Help], Texts(row));
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_check_box_shows_whether_Keep_awake_is_on(bool on)
    {
        _keepAwake.State = new KeepAwakeState(on, false, null);

        var row = Row();

        Assert.Equal(on, Box(row).IsChecked);
        Assert.Empty(_keepAwake.SetOnCalls);
    }

    [WpfFact]
    public void The_check_box_stays_on_while_the_screen_lock_pauses_Keep_awake()
    {
        _keepAwake.State = new KeepAwakeState(true, true, null);

        Assert.True(Box(Row()).IsChecked);
    }

    [WpfFact]
    public void Switching_the_check_box_on_and_off_passes_each_value_on_at_once()
    {
        var row = Row();

        Box(row).IsChecked = true;
        Box(row).IsChecked = false;

        Assert.Equal([true, false], _keepAwake.SetOnCalls);
        Assert.False(Box(row).IsChecked);
    }

    [WpfFact]
    public void A_failed_save_puts_the_check_box_back_shows_why_in_the_error_color_and_logs_it()
    {
        var failure = new IOException("The setting file is read-only.");
        _keepAwake.SetOnFailure = failure;
        var row = Row();

        Box(row).IsChecked = true;

        Assert.False(Box(row).IsChecked);
        Assert.Same(_errorBrush, Block(row, "The setting file is read-only.").Foreground);
        Assert.Contains(_log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));
    }

    [WpfFact]
    public void A_later_successful_save_hides_the_failure()
    {
        _keepAwake.SetOnFailure = new IOException("The setting file is read-only.");
        var row = Row();
        Box(row).IsChecked = true;
        _keepAwake.SetOnFailure = null;

        Box(row).IsChecked = true;

        Assert.DoesNotContain("The setting file is read-only.", Texts(row));
        Assert.True(Box(row).IsChecked);
    }

    [WpfFact]
    public void A_refusal_by_Windows_shows_in_the_error_color_and_keeps_the_check_box_on()
    {
        _keepAwake.State = new KeepAwakeState(true, false, "Windows refused to keep the screen on.");

        var row = Row();

        Assert.True(Box(row).IsChecked);
        Assert.Same(_errorBrush, Block(row, "Windows refused to keep the screen on.").Foreground);
    }

    [WpfFact]
    public void Follows_Keep_awake_while_loaded_without_switching_it()
    {
        var row = Row();
        Load(row);

        _keepAwake.Publish(new KeepAwakeState(true, false, null));
        Assert.True(Box(row).IsChecked);

        _keepAwake.Publish(new KeepAwakeState(true, false, "Windows refused to keep the screen on."));
        Assert.Contains("Windows refused to keep the screen on.", Texts(row));

        _keepAwake.Publish(new KeepAwakeState(true, false, null));
        Assert.DoesNotContain("Windows refused to keep the screen on.", Texts(row));

        Assert.Empty(_keepAwake.SetOnCalls);
    }

    [WpfFact]
    public void Stops_following_Keep_awake_once_unloaded()
    {
        var row = Row();
        Load(row);
        row.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        _keepAwake.Publish(new KeepAwakeState(true, false, null));

        Assert.False(Box(row).IsChecked);
    }

    [WpfFact]
    public void Working_switches_log_no_problem()
    {
        var row = Row();

        Box(row).IsChecked = true;

        _log.Events.AssertNoProblems();
    }
}
