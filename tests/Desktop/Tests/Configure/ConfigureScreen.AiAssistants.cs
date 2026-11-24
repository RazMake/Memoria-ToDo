using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.McpServer;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// How a test reads the AI assistants card in Configure's Application section: its box, its texts, check box, each
/// assistant's read-only lines, and Copy buttons. The server's state is the shared fake of the MCP server's status.
/// </summary>
internal sealed partial class ConfigureScreen
{
    public const string AiSwitchText = "Let AI assistants on this PC use Memoria-ToDo";

    /// <summary>The shared fake of the MCP server's status the window shows; set it before <see cref="Open"/>, or publish while open.</summary>
    public FakeMcpServerStatus McpServer => services.McpServer;

    /// <summary>The state changes while the window is open, as when the server starts listening.</summary>
    public void PublishMcpServer(McpServerState state)
    {
        McpServer.Publish(state);
        Settle();
    }

    /// <summary>The box that holds the AI assistants card: the nearest border around its "AI assistants" title.</summary>
    public Border AiCard
    {
        get
        {
            Settle();
            DependencyObject element = CardBlocks(Window).First(block => OpenWindows.TextOf(block) == "AI assistants");
            while (element is not Border)
            {
                element = LogicalTreeHelper.GetParent(element) ?? throw new InvalidOperationException("The card's lines sit in no box.");
            }

            return (Border)element;
        }
    }

    /// <summary>The texts the card shows under its title, in text blocks, top to bottom.</summary>
    public IReadOnlyList<string> AiTexts =>
        [.. CardBlocks(AiCard).Where(block => block.IsVisible).Select(OpenWindows.TextOf).Where(text => text.Length > 0).Skip(1)];

    /// <summary>The visible text block of the card that starts with the words.</summary>
    public TextBlock AiBlock(string start) =>
        CardBlocks(AiCard).Single(block => block.IsVisible && OpenWindows.TextOf(block).StartsWith(start, StringComparison.Ordinal));

    /// <summary>The check box that lets assistants in.</summary>
    public CheckBox AiBox =>
        OpenWindows.Descendants(AiCard).OfType<CheckBox>().Single(box => box.Content as string == AiSwitchText);

    /// <summary>The read-only boxes with each assistant's lines, top to bottom.</summary>
    public IReadOnlyList<TextBox> AiLineBoxes => [.. OpenWindows.Descendants(AiCard).OfType<TextBox>()];

    /// <summary>The Copy buttons of the card, top to bottom: one for each assistant, in the server's order.</summary>
    public IReadOnlyList<Button> AiCopyButtons =>
        [.. OpenWindows.Descendants(AiCard).OfType<Button>().Where(button => button.Content as string == "Copy")];

    /// <summary>Switches the card's check box as a click does.</summary>
    public void SwitchAi()
    {
        ((IToggleProvider)new CheckBoxAutomationPeer(AiBox).GetPattern(PatternInterface.Toggle)).Toggle();
        Settle();
    }

    /// <summary>Clicks an assistant's Copy button, as the mouse does.</summary>
    public void CopyAi(int index)
    {
        OpenWindows.Click(AiCopyButtons[index]);
        Settle();
    }
}
