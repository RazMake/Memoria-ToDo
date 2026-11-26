using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The AI assistants row of Configure's Behavior section, built in memory with no window: its check box, the Examples…
/// button on the check box's line, the help line, the status line, and what a failed switch shows. What each state says
/// and the failures are the <see cref="AiAssistantsSettingTests"/>'; these tests check that the row shows them, calls the
/// MCP server, and asks for the Examples dialog. The dialog's own content is checked in Configure's UX paths.
/// </summary>
[Unit]
public class AiAssistantsRowTests
{
    private const string Running = "Running. AI assistants on this PC can use Memoria-ToDo.";
    private const string ErrorKey = "SystemFillColorCriticalBrush";

    private readonly FakeMcpServerStatus _server = new();
    private readonly FakeSetupActions _actions = new();
    private readonly InMemoryLogSink _log = new();
    private readonly SolidColorBrush _errorBrush = new(Colors.Red);
    private int _examplesRequests;

    private AiAssistantsRow Card()
    {
        var card = new AiAssistantsRow(new AiAssistantsSetting(_server, _actions, _log.CreateLogger()), _server, _ => _examplesRequests++);
        card.Resources[ErrorKey] = _errorBrush;
        return card;
    }

    private static IReadOnlyList<string> Texts(AiAssistantsRow card) =>
        [.. OpenWindows.Descendants(card).OfType<TextBlock>()
            .Where(block => block.Visibility == Visibility.Visible)
            .Select(OpenWindows.TextOf).Where(text => text.Length > 0)];

    private static TextBlock Block(AiAssistantsRow card, string start) =>
        OpenWindows.Descendants(card).OfType<TextBlock>()
            .Single(block => block.Visibility == Visibility.Visible && OpenWindows.TextOf(block).StartsWith(start, StringComparison.Ordinal));

    private static CheckBox Box(AiAssistantsRow card) =>
        OpenWindows.Descendants(card).OfType<CheckBox>().Single(box => box.Content as string == "Let AI assistants on this PC use Memoria-ToDo");

    private static Button Examples(AiAssistantsRow card) =>
        OpenWindows.Descendants(card).OfType<Button>().Single(button => button.Content as string == "Examples…");

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    private static void Load(AiAssistantsRow card) => card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

    [WpfFact]
    public void Shows_the_help_line_first_and_then_the_status_line()
    {
        var card = Card();

        Assert.Equal(
            "Assistants such as GitHub Copilot, Claude, and Codex can read and change your tasks, contacts, and settings. "
            + "Each asks you before it uses a tool, as its own settings say.",
            Texts(card)[0]);
        Assert.Equal(Running, Texts(card)[1]);
    }

    [WpfFact]
    public void Has_no_card_title_assistant_lines_or_Copy_buttons()
    {
        var card = Card();

        Assert.DoesNotContain("AI assistants", Texts(card));
        Assert.Empty(OpenWindows.Descendants(card).OfType<TextBox>());
        Assert.DoesNotContain(OpenWindows.Descendants(card).OfType<Button>(), button => button.Content as string == "Copy");
    }

    [WpfFact]
    public void The_Examples_button_sits_on_the_check_boxs_line_after_it_with_the_hand_pointer()
    {
        var card = Card();
        card.Measure(new Size(700, double.PositiveInfinity));
        card.Arrange(new Rect(card.DesiredSize));

        var box = Box(card);
        var examples = Examples(card);

        Assert.Equal(Cursors.Hand, examples.Cursor);
        Assert.True(examples.TranslatePoint(new Point(0, 0), card).X >= box.TranslatePoint(new Point(box.ActualWidth, 0), card).X);
        Assert.True(examples.TranslatePoint(new Point(0, 0), card).Y < box.TranslatePoint(new Point(0, box.ActualHeight), card).Y);
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Examples_asks_for_the_dialog_whether_the_switch_is_on_or_off_and_leaves_the_server_alone(bool on)
    {
        _server.State = new McpServerState(on, on, null);
        var card = Card();

        Click(Examples(card));

        Assert.Equal(1, _examplesRequests);
        Assert.Empty(_server.SetOnCalls);
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_check_box_shows_whether_the_server_is_on(bool on)
    {
        _server.State = new McpServerState(on, on, null);

        var card = Card();

        Assert.Equal(on, Box(card).IsChecked);
        Assert.Empty(_server.SetOnCalls);
    }

    [WpfFact]
    public void Switching_the_check_box_off_calls_the_server_and_shows_the_off_status()
    {
        var card = Card();

        Box(card).IsChecked = false;

        Assert.Equal([false], _server.SetOnCalls);
        Assert.False(Box(card).IsChecked);
        Assert.Contains("Off. AI assistants can't reach Memoria-ToDo.", Texts(card));
    }

    [WpfFact]
    public void A_failed_switch_puts_the_check_box_back_and_shows_why_in_the_error_color()
    {
        var card = Card();
        _server.SetOnFailure = new IOException("The switch file is read-only.");

        Box(card).IsChecked = false;

        Assert.True(Box(card).IsChecked);
        Assert.Same(_errorBrush, Block(card, "The switch file is read-only.").Foreground);
    }

    [WpfFact]
    public void A_later_successful_switch_hides_the_failure()
    {
        var card = Card();
        _server.SetOnFailure = new IOException("The switch file is read-only.");
        Box(card).IsChecked = false;
        _server.SetOnFailure = null;

        Box(card).IsChecked = false;

        Assert.DoesNotContain("The switch file is read-only.", Texts(card));
        Assert.False(Box(card).IsChecked);
    }

    [WpfFact]
    public void The_status_line_follows_the_server_while_the_card_is_loaded()
    {
        _server.State = new McpServerState(true, false, null);
        var card = Card();
        Load(card);
        Assert.Contains("Starting…", Texts(card));

        _server.Publish(new McpServerState(true, true, null));
        Assert.Contains(Running, Texts(card));

        _server.Publish(new McpServerState(true, false, "The pipe is taken by another program."));
        Assert.Same(_errorBrush, Block(card, "The pipe is taken by another program.").Foreground);
        Assert.DoesNotContain(Running, Texts(card));
    }

    [WpfFact]
    public void The_check_box_follows_the_server_while_the_card_is_loaded_without_calling_it()
    {
        var card = Card();
        Load(card);

        _server.Publish(new McpServerState(false, false, null));

        Assert.False(Box(card).IsChecked);
        Assert.Empty(_server.SetOnCalls);
    }

    [WpfFact]
    public void Stops_following_the_server_once_it_is_unloaded()
    {
        var card = Card();
        Load(card);
        card.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        _server.Publish(new McpServerState(false, false, null));

        Assert.True(Box(card).IsChecked);
        Assert.Contains(Running, Texts(card));
    }

    [WpfFact]
    public void Working_switches_and_examples_log_no_problem()
    {
        var card = Card();

        Box(card).IsChecked = false;
        Click(Examples(card));

        _log.Events.AssertNoProblems();
    }
}
