using System.Text.RegularExpressions;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>
/// Editing in the Add window: moving between the fields, formatting and snippet keys, the snippet, task and color lists,
/// and images. The rules behind each key and list are in the unit tests of the formatting keys, list keys, snippet keys,
/// the pickers and the image insertion.
/// </summary>
[UX("AddWindow")]
[Collection(ScreenTests.Collection)]
public class AddWindowEditingTests
{
    private static readonly Regex ImageLink = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));

    [WpfFact]
    public void Path_9_fields_keys_the_snippet_task_and_color_lists_and_images_edit_the_Description()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Services.AddTaskTitled("Call mom");
        screen.Show();

        // Enter in the Title moves to the Description, and the Description grows with its lines.
        screen.Fill("Buy milk");
        screen.Press(Key.Enter);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Equal("Buy milk", screen.Editor.Value.Title);
        screen.Fill("Title", "one line");
        var few = screen.DescriptionHeight;
        screen.Fill("Title", string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"line {i}")));
        Assert.True(screen.DescriptionHeight > few, "The Description did not grow with its content.");

        // Formatting, list and snippet keys.
        screen.Edit(EditorFields.Description, "a b c", 2, 1);
        screen.Press(Key.LeftCtrl, Key.B);
        Assert.Equal("a **b** c", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.True(screen.IsShowing);

        screen.Edit(EditorFields.Description, "- a", 3);
        screen.Press(Key.Enter);
        Assert.Equal("- a\n- ", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.DoesNotContain(screen.Saved, item => item.Title == "Title");

        screen.Services.Time.Advance(TimeSpan.FromHours(10.5));
        screen.Edit(EditorFields.Description, "on ", 3);
        screen.Press(Key.LeftCtrl, Key.OemSemicolon);
        Assert.Equal("on " + screen.LocalNow.ToString("yyyy-MM-dd"), screen.TextOf(EditorFields.Description));

        screen.Services.Clipboard.Content = "https://example.com/docs";
        screen.Edit(EditorFields.Description, "see docs now", 4, 4);
        screen.Press(Key.LeftCtrl, Key.K);
        Assert.Equal("see [docs](https://example.com/docs) now", screen.TextOf(EditorFields.Description));

        // The snippet list opens only in the Description.
        screen.Edit(EditorFields.Title, "", 0);
        screen.Type("/");
        Assert.Empty(screen.ShownSnippets);

        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/");
        Assert.Equal(
            ["Date stamp", "Date and time stamp", "Note", "Tip", "Warning", "Link", "Image", "Code block", "Task reference", "Table", "Horizontal line"],
            screen.ShownSnippets);
        Assert.Equal("/", screen.TextOf(EditorFields.Description));
        screen.Type("no");
        screen.Press(Key.Down);
        screen.Press(Key.Enter);
        Assert.Equal("> [!NOTE]\n> ", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);

        screen.Edit(EditorFields.Description, "a ", 2);
        screen.Type("/ti");
        screen.ClickSnippet("Tip");
        Assert.Equal("a \n> [!TIP]\n> ", screen.TextOf(EditorFields.Description));
        Assert.Empty(screen.ShownSnippets);

        // The task list lists open tasks newest first; a filter, Down and Enter choose.
        screen.Edit(EditorFields.Description, "see  now", 4);
        screen.PressCtrl(true, Key.R);
        Assert.Equal(["Call mom", "Buy milk"], screen.ShownTaskReferences);
        screen.ClickTaskReference("Buy milk");
        Assert.Equal($"see [Buy milk](task:{task.Id}) now", screen.TextOf(EditorFields.Description));
        Assert.Empty(screen.ShownTaskReferences);
        Assert.Equal(EditorFields.Description, screen.FocusedField);

        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);
        screen.Type("mil");
        screen.Press(Key.Down);
        screen.Press(Key.Enter);
        Assert.Equal($"[Buy milk](task:{task.Id})", screen.TextOf(EditorFields.Description));

        // The color list wraps the selection.
        screen.Edit(EditorFields.Description, "do urgent now", 3, 6);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.C);
        Assert.Equal(["red", "orange", "green", "blue", "purple", "gray"], screen.ShownColors);
        screen.ClickColor("red");
        Assert.Equal("do :red[urgent] now", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Empty(screen.ShownColors);

        // Images: pasted, chosen and dropped, and one that cannot be kept.
        var png = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        screen.Edit(EditorFields.Description, "ab", 1);
        screen.Services.Clipboard.Image = png;
        screen.PressCtrl(false, Key.V);
        var link = Assert.Single(ImageLink.Matches(screen.TextOf(EditorFields.Description)));
        Assert.Equal("a" + link.Value + "b", screen.TextOf(EditorFields.Description));
        Assert.True(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link.Groups[2].Value)));

        screen.Edit(EditorFields.Description, "", 0);
        screen.Services.Clipboard.Image = null;
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("Cat photo.png", png);
        screen.PressCtrl(true, Key.I);
        Assert.Equal("Cat photo", Assert.Single(ImageLink.Matches(screen.TextOf(EditorFields.Description))).Groups[1].Value);

        screen.Edit(EditorFields.Description, "ab", 1);
        var dropped = SimulatedDrop.Files(EditorFields.Field(screen.Window, EditorFields.Description), screen.Services.PutImageFile("Cat.png", png));
        Assert.True(dropped);
        Assert.Equal("Cat", Assert.Single(ImageLink.Matches(screen.TextOf(EditorFields.Description))).Groups[1].Value);

        screen.Edit(EditorFields.Description, "ab", 1);
        var before = screen.Texts;
        screen.Services.Clipboard.Image = null;
        screen.Services.Clipboard.ImageFailure = new System.Runtime.InteropServices.ExternalException("The clipboard is busy.");
        screen.PressCtrl(false, Key.V);
        Assert.Contains(screen.Texts.Except(before), text => text.Contains("image", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("ab", screen.TextOf(EditorFields.Description));
        Assert.True(screen.IsShowing);

        // Tab outside a list moves to the tag, and a second Esc after a list closes the window.
        screen.Edit(EditorFields.Description, "plain", 5);
        screen.Press(Key.Tab);
        Assert.Equal("plain", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.TagPill, screen.FocusedField);

        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/ti");
        screen.Press(Key.Escape);
        Assert.Empty(screen.ShownSnippets);
        Assert.True(screen.IsShowing);
        Assert.Equal("/ti", screen.TextOf(EditorFields.Description));
        screen.Press(Key.Escape);
        Assert.False(screen.IsShowing);
    }
}
