using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The AI assistants card of Configure's Application section, built in memory with no window: its help line, its check box,
/// the status line, each assistant's lines and Copy button, and what a failed switch or copy shows. What each state says
/// and the failures are the <see cref="AiAssistantsSettingTests"/>'; these tests check that the card shows them and
/// calls the MCP server and the clipboard.
/// </summary>
[Unit]
public class AiAssistantsCardTests
{
    private const string Running = "Running. AI assistants on this PC can use Memoria-ToDo.";
    private const string ErrorKey = "SystemFillColorCriticalBrush";

    private static readonly McpConnection[] Connections =
        [new("VS Code", "vs code line 1\nvs code line 2"), new("Claude Desktop", "claude desktop lines"), new("Codex", "codex lines")];

    private readonly FakeMcpServerStatus _server = new() { Connections = Connections };
    private readonly FakeSetupActions _actions = new();
    private readonly InMemoryLogSink _log = new();
    private readonly SolidColorBrush _errorBrush = new(Colors.Red);

    private AiAssistantsCard Card()
    {
        var card = new AiAssistantsCard(new AiAssistantsSetting(_server, _actions, _log.CreateLogger()), _server);
        card.Resources[ErrorKey] = _errorBrush;
        return card;
    }

    private static IReadOnlyList<string> Texts(AiAssistantsCard card) =>
        [.. OpenWindows.Descendants(card).OfType<TextBlock>()
            .Where(block => block.Visibility == Visibility.Visible)
            .Select(OpenWindows.TextOf).Where(text => text.Length > 0)];

    private static TextBlock Block(AiAssistantsCard card, string start) =>
        OpenWindows.Descendants(card).OfType<TextBlock>()
            .Single(block => block.Visibility == Visibility.Visible && OpenWindows.TextOf(block).StartsWith(start, StringComparison.Ordinal));

    private static CheckBox Box(AiAssistantsCard card) =>
        OpenWindows.Descendants(card).OfType<CheckBox>().Single(box => box.Content as string == "Let AI assistants on this PC use Memoria-ToDo");

    private static IReadOnlyList<TextBox> LineBoxes(AiAssistantsCard card) => [.. OpenWindows.Descendants(card).OfType<TextBox>()];

    private static IReadOnlyList<Button> CopyButtons(AiAssistantsCard card) =>
        [.. OpenWindows.Descendants(card).OfType<Button>().Where(button => button.Content as string == "Copy")];

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    private static void Load(AiAssistantsCard card) => card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

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
    public void Shows_each_assistants_name_and_lines_in_the_servers_order_with_a_Copy_button()
    {
        var card = Card();

        Assert.Equal(["VS Code", "Claude Desktop", "Codex"], Texts(card).Where(text => Connections.Any(c => c.Assistant == text)));
        Assert.Equal(Connections.Select(c => c.Text), LineBoxes(card).Select(box => box.Text));
        Assert.All(LineBoxes(card), box => Assert.True(box.IsReadOnly));
        Assert.Equal(3, CopyButtons(card).Count);
    }

    [WpfFact]
    public void Copy_copies_that_assistants_lines()
    {
        var card = Card();

        Click(CopyButtons(card)[1]);

        Assert.Equal(["claude desktop lines"], _actions.CopiedTexts);
    }

    [WpfFact]
    public void A_failed_copy_shows_why_in_the_error_color_and_keeps_the_switch()
    {
        var card = Card();
        _actions.CopyFailure = new InvalidOperationException("Clipboard is busy.");

        Click(CopyButtons(card)[0]);

        Assert.Same(_errorBrush, Block(card, "Clipboard is busy.").Foreground);
        Assert.True(Box(card).IsChecked);
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
    public void Working_switches_and_copies_log_no_problem()
    {
        var card = Card();

        Box(card).IsChecked = false;
        Click(CopyButtons(card)[0]);

        _log.Events.AssertNoProblems();
    }
}
