using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests.Examples;

/// <summary>
/// The Connect an AI assistant dialog that Configure's Examples… button opens, in the paths a user takes: reading each
/// assistant's lines and copying them, a copy that fails, and closing it by its button, Esc, and the Close window shortcut.
/// It's a modal dialog owned by Configure with a line on where the text goes, then each assistant's name, its read-only
/// lines, and a Copy button. What the MCP server lists is its own tests'; the dialog's own messages and logging are checked here.
/// </summary>
[UX("Examples")]
[Collection(ScreenTests.Collection)]
public class ExamplesPathTests
{
    private const string Intro =
        "Paste an assistant's text into its MCP settings file, or run it in a terminal. "
        + "Connect an AI assistant, in the app's user guide, says where each file is.";

    private static readonly McpConnection[] Connections =
        [new("VS Code", "vs code line 1\nvs code line 2"), new("Claude Desktop", "claude desktop lines"), new("Codex", "codex lines")];

    private static IReadOnlyList<Button> CopyButtons(Window dialog) =>
        [.. OpenWindows.Descendants(dialog).OfType<Button>().Where(button => button.Content as string == "Copy")];

    private static IReadOnlyList<TextBox> LineBoxes(Window dialog) => [.. OpenWindows.Descendants(dialog).OfType<TextBox>()];

    private static ConfigureScreen OpenConfigure()
    {
        var screen = new ConfigureScreen();
        screen.McpServer.Connections = Connections;
        screen.Open();
        screen.ClickTitle("Behavior");
        return screen;
    }

    [WpfFact]
    public void Examples_opens_a_modal_dialog_over_Configure_with_each_assistants_lines_that_copy_with_the_Copy_button_and_closes_with_Close()
    {
        using var screen = OpenConfigure();
        screen.SwitchAi(); // The switch is off, and Examples still works.
        Assert.False(screen.AiBox.IsChecked);
        screen.McpServer.Connections = [.. Connections, new("GitHub Copilot CLI", "copilot lines")]; // Read when the dialog opens.
        string[] names = ["VS Code", "Claude Desktop", "Codex", "GitHub Copilot CLI"];

        var driver = screen.OpenExamples(dialog =>
        {
            Assert.Same(screen.Window, dialog.Owner);
            Assert.Contains(Intro, OpenWindows.TextsIn(dialog));
            Assert.Equal(names, OpenWindows.TextsIn(dialog).Where(names.Contains));
            Assert.Equal(["vs code line 1\nvs code line 2", "claude desktop lines", "codex lines", "copilot lines"], LineBoxes(dialog).Select(box => box.Text));
            Assert.All(LineBoxes(dialog), box =>
            {
                Assert.True(box.IsReadOnly);
                Assert.Equal("Consolas", box.FontFamily.Source);
            });
            Assert.Equal(4, CopyButtons(dialog).Count);

            OpenWindows.Click(CopyButtons(dialog)[2]);
            Assert.Equal(["codex lines"], screen.SetupActions.CopiedTexts);
            Assert.True(dialog.IsVisible);

            OpenWindows.Click(dialog, "Close");
            Assert.False(dialog.IsVisible, "Close didn't close the dialog.");
        });

        Assert.False(driver.ReturnedWhileShowing);
        Assert.True(driver.Centered);
        Assert.True(screen.Window.IsVisible);
        screen.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public void A_failed_copy_shows_its_reason_at_the_dialogs_bottom_in_the_error_color_keeps_the_dialog_open_and_logs_it()
    {
        using var screen = OpenConfigure();
        var failure = new InvalidOperationException("Clipboard is busy.");
        screen.SetupActions.CopyFailure = failure;

        screen.OpenExamples(dialog =>
        {
            OpenWindows.UseTheme(dialog, dark: false);
            OpenWindows.Click(CopyButtons(dialog)[0]);

            Assert.True(dialog.IsVisible);
            var message = OpenWindows.Descendants(dialog).OfType<TextBlock>()
                .Single(block => block.IsVisible && OpenWindows.TextOf(block).StartsWith("Clipboard is busy.", StringComparison.Ordinal));
            Assert.Equal(((SolidColorBrush)dialog.FindResource("SystemFillColorCriticalBrush")).Color, ((SolidColorBrush)message.Foreground).Color);
            var lastCopy = CopyButtons(dialog)[^1];
            dialog.UpdateLayout(); // This runs in the dialog's Loaded handler, where WPF holds the next layout pass until it returns.
            Assert.True(message.TranslatePoint(new Point(0, 0), dialog).Y > lastCopy.TranslatePoint(new Point(0, 0), dialog).Y);
            Assert.Empty(screen.SetupActions.CopiedTexts);
        });

        Assert.Contains(screen.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));
    }

    [WpfFact]
    public void Esc_and_the_Close_window_shortcut_close_the_dialog_and_leave_Configure_open()
    {
        using var screen = OpenConfigure();
        screen.CloseKeys.Set(ModifierKeys.Shift, Key.F9);

        screen.OpenExamples(dialog =>
        {
            OpenWindows.PressEscape(dialog);
            Assert.False(dialog.IsVisible, "Esc didn't close the dialog.");
        });
        Assert.True(screen.Window.IsVisible);

        screen.OpenExamples(dialog =>
        {
            SimulatedKey.Chord(dialog, Key.LeftShift, Key.F9);
            Assert.False(dialog.IsVisible, "The Close window shortcut didn't close the dialog.");
        });
        Assert.True(screen.Window.IsVisible);
        screen.Log.Events.AssertNoProblems();
    }
}
