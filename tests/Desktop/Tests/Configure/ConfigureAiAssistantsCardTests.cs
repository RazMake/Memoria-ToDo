using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.McpServer;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The AI assistants card in Configure's Application section, as a user sees it in its common scenarios: its place and lines,
/// the switch, the status line, and the Copy buttons. What each state says and the failures are the
/// <see cref="AiAssistantsSettingTests"/>'; these tests check that the card shows them and calls the MCP server and the clipboard.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureAiAssistantsCardTests
{
    private const string ErrorKey = "SystemFillColorCriticalBrush";

    private static readonly McpConnection[] Connections =
        [new("VS Code", "vs code line 1\nvs code line 2"), new("Claude Desktop", "claude desktop lines"), new("Codex", "codex lines")];

    [WpfFact]
    public void The_card_is_the_third_card_in_Application_below_Shortcuts_and_above_Startup()
    {
        using var screen = Open();

        Assert.True(screen.TextTop("Shortcuts") < screen.TextTop("AI assistants"), "The card isn't below the Shortcuts card.");
        Assert.True(screen.TextTop("AI assistants") < screen.TitleTop("Startup"), "The card isn't above Startup's title.");
    }

    [WpfFact]
    public void The_card_shows_the_check_box_with_its_help_line_and_the_status_line()
    {
        using var screen = Open();

        Assert.Equal(
            "Assistants such as GitHub Copilot, Claude, and Codex can read and change your tasks, contacts, and settings. "
            + "Each asks you before it uses a tool, as its own settings say.",
            screen.AiTexts[0]);
        Assert.Contains("Running. AI assistants on this PC can use Memoria-ToDo.", screen.AiTexts);
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_check_box_shows_whether_the_server_is_on(bool on)
    {
        using var screen = Open(new McpServerState(on, on, null));

        Assert.Equal(on, screen.AiBox.IsChecked);
    }

    [WpfFact]
    public void Switching_the_check_box_off_calls_the_server_at_once()
    {
        using var screen = Open();

        screen.SwitchAi();

        Assert.Equal([false], screen.McpServer.SetOnCalls);
        Assert.False(screen.AiBox.IsChecked);
        Assert.Contains("Off. AI assistants can't reach Memoria-ToDo.", screen.AiTexts);
    }

    [WpfFact]
    public void A_failed_switch_puts_the_check_box_back_and_shows_why_in_the_error_color()
    {
        using var screen = Open();
        screen.McpServer.SetOnFailure = new IOException("The switch file is read-only.");

        screen.SwitchAi();

        Assert.True(screen.AiBox.IsChecked);
        var line = screen.AiBlock("The switch file is read-only.");
        Assert.Equal(screen.ThemeColor(ErrorKey), ConfigureScreen.ColorOf(line));
    }

    [WpfFact]
    public void Shows_each_assistants_name_and_lines_in_the_servers_order_with_a_Copy_button()
    {
        using var screen = Open();

        Assert.Equal(["VS Code", "Claude Desktop", "Codex"], screen.AiTexts.Where(text => Connections.Any(c => c.Assistant == text)));
        Assert.Equal(Connections.Select(c => c.Text), screen.AiLineBoxes.Select(box => box.Text));
        Assert.All(screen.AiLineBoxes, box => Assert.True(box.IsReadOnly));
        Assert.Equal(3, screen.AiCopyButtons.Count);
    }

    [WpfFact]
    public void Copy_copies_that_assistants_lines()
    {
        using var screen = Open();

        screen.CopyAi(1);

        Assert.Equal(["claude desktop lines"], screen.SetupActions.CopiedTexts);
    }

    [WpfFact]
    public void A_failed_copy_shows_why_in_the_error_color()
    {
        using var screen = Open();
        screen.SetupActions.CopyFailure = new InvalidOperationException("Clipboard is busy.");

        screen.CopyAi(0);

        var line = screen.AiBlock("Clipboard is busy.");
        Assert.Equal(screen.ThemeColor(ErrorKey), ConfigureScreen.ColorOf(line));
        Assert.True(screen.AiBox.IsChecked);
    }

    [WpfFact]
    public void The_status_line_follows_the_server_while_the_window_is_open()
    {
        using var screen = Open(new McpServerState(true, false, null));
        Assert.Contains("Starting…", screen.AiTexts);

        screen.PublishMcpServer(new McpServerState(true, true, null));
        Assert.Contains("Running. AI assistants on this PC can use Memoria-ToDo.", screen.AiTexts);

        screen.PublishMcpServer(new McpServerState(true, false, "The pipe is taken by another program."));
        var failure = screen.AiBlock("The pipe is taken by another program.");
        Assert.Equal(screen.ThemeColor(ErrorKey), ConfigureScreen.ColorOf(failure));
        Assert.DoesNotContain("Running. AI assistants on this PC can use Memoria-ToDo.", screen.AiTexts);
    }

    [WpfFact]
    public void The_check_box_follows_the_server_while_the_window_is_open()
    {
        using var screen = Open();

        screen.PublishMcpServer(new McpServerState(false, false, null));

        Assert.False(screen.AiBox.IsChecked);
        Assert.Empty(screen.McpServer.SetOnCalls);
    }

    private static ConfigureScreen Open(McpServerState? state = null)
    {
        var screen = new ConfigureScreen();
        screen.McpServer.State = state ?? new McpServerState(true, true, null);
        screen.McpServer.Connections = Connections;
        screen.Open();
        OpenWindows.UseTheme(screen.Window, dark: false);
        return screen;
    }
}
