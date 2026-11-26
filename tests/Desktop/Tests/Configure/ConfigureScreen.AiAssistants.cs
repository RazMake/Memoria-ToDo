using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// How a test reads the rows of Configure's Behavior section: the AI assistants row's check box, texts, and Examples…
/// button, the Prevent screen saver check box, and the Examples dialog the button opens. The server's state is the
/// shared fake of the MCP server's status, and Keep awake's the shared fake of its status.
/// </summary>
internal sealed partial class ConfigureScreen
{
    public const string AiSwitchText = "Let AI assistants on this PC use Memoria-ToDo";

    /// <summary>The shared fake of the MCP server's status the window shows; set it before <see cref="Open"/>, or publish while open.</summary>
    public FakeMcpServerStatus McpServer => services.McpServer;

    /// <summary>The check box that lets assistants in; its section may be closed.</summary>
    public CheckBox AiBox =>
        OpenWindows.Descendants(Window).OfType<CheckBox>().Single(box => box.Content as string == AiSwitchText);

    /// <summary>The row's root: the nearest logical ancestor of its check box that also holds the row's help line.</summary>
    public DependencyObject AiRow
    {
        get
        {
            Settle();
            DependencyObject element = AiBox;
            while (!CardBlocks(element).Any(block => OpenWindows.TextOf(block).StartsWith("Assistants such as", StringComparison.Ordinal)))
            {
                element = LogicalTreeHelper.GetParent(element) ?? throw new InvalidOperationException("The check box and its help line share no parent.");
            }

            return element;
        }
    }

    /// <summary>The texts the row shows, in text blocks, top to bottom.</summary>
    public IReadOnlyList<string> AiTexts =>
        [.. CardBlocks(AiRow).Where(block => block.IsVisible).Select(OpenWindows.TextOf).Where(text => text.Length > 0)];

    /// <summary>The Examples… button of the row.</summary>
    public Button ExamplesButton =>
        OpenWindows.Descendants(AiRow).OfType<Button>().Single(button => button.Content as string == "Examples…");

    /// <summary>Switches the row's check box as a click does.</summary>
    public void SwitchAi()
    {
        Reveal(AiBox);
        ((IToggleProvider)new CheckBoxAutomationPeer(AiBox).GetPattern(PatternInterface.Toggle)).Toggle();
        Settle();
    }

    /// <summary>The Close window keys the window was given; set them to try the shortcut that closes a dialog.</summary>
    public CloseWindowKeys CloseKeys => services.CloseWindowKeys;

    /// <summary>The theme-aware set of the windows this screen's window opened, for driving a dialog it opens.</summary>
    public OpenWindows OpenedWindows => windows;

    /// <summary>The Prevent screen saver check box.</summary>
    public CheckBox KeepAwakeBox =>
        OpenWindows.Descendants(Window).OfType<CheckBox>().Single(box => box.Content as string == "Prevent screen saver");

    /// <summary>The shared fake of Keep awake's status the window shows; set its state before <see cref="Open"/>, or publish while open.</summary>
    public FakeKeepAwakeStatus KeepAwake => services.KeepAwake;

    /// <summary>Switches the Prevent screen saver check box as a click does.</summary>
    public void SwitchKeepAwake()
    {
        Reveal(KeepAwakeBox);
        ((IToggleProvider)new CheckBoxAutomationPeer(KeepAwakeBox).GetPattern(PatternInterface.Toggle)).Toggle();
        Settle();
    }

    /// <summary>
    /// Clicks Examples… as the mouse does and drives the dialog it opens, as <see cref="ModalDialogDriver.Run"/> says.
    /// The click is a call that returns only when the dialog closes. Fails the test when the click opens no dialog.
    /// </summary>
    public ModalDialogDriver OpenExamples(Action<Window> act)
    {
        var driver = new ModalDialogDriver(Window, windows);
        Reveal(AiBox);
        driver.Run("Connect an AI assistant", () => OpenWindows.Click(ExamplesButton), act);
        Assert.True(driver.Dialog is not null, "Examples… opened no dialog.");
        return driver;
    }
}
