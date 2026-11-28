using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What the real editor of the Title and the Description offers the hover card for a point: the card of the contact whose mention chip is
/// under the point, boxed by that chip, and nothing anywhere else. The chip, the card and the hover card are tested apart; the editor is laid out but never shown.
/// </summary>
[Unit]
public class MentionCardInEditorTests
{
    private const string Text = "Call [Ana](contact:ana \"{fullName}\") then [Bob](contact:bob \"{fullName}\") or [Zed](contact:zed \"{fullName}\") today";

    private readonly EditorServices _services = new();

    public MentionCardInEditorTests()
    {
        _services.UseContacts(["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop"),
            ContactFiles.Person("bob", "Bob Rus"));
    }

    // A real editor holding the text and laid out, never shown.
    private StyledTextEditor Editor(MarkdownField field)
    {
        var editor = new StyledTextEditor(field.ToString(), field, _services.Theme, _services.Contacts, _services.Pointer, Serilog.Core.Logger.None)
        {
            Text = Text,
        };
        editor.Measure(new Size(600, 300));
        editor.Arrange(new Rect(0, 0, 600, 300));
        editor.UpdateLayout();
        editor.TextArea.TextView.EnsureVisualLines();
        Assert.NotEmpty(editor.TextArea.TextView.VisualLines);
        return editor;
    }

    // The chips in the text view, left to right.
    private static List<MentionChip> ChipsIn(StyledTextEditor editor) =>
        [.. VisualTree.Visuals(editor.TextArea.TextView).OfType<MentionChip>().OrderBy(chip => chip.TranslatePoint(default, editor).X)];

    private static Point Middle(StyledTextEditor editor, MentionChip chip) =>
        chip.TranslatePoint(new Point(chip.ActualWidth / 2, chip.ActualHeight / 2), editor);

    private static bool Shows(FrameworkElement card, string text) =>
        VisualTree.Visuals(card).OfType<TextBlock>().Any(block => OpenWindows.TextOf(block).Contains(text, StringComparison.Ordinal));

    [WpfTheory]
    [InlineData(MarkdownField.Title)]
    [InlineData(MarkdownField.Description)]
    public async Task Resting_on_a_chip_gives_the_card_of_that_chips_contact_boxed_by_the_chip(MarkdownField field)
    {
        var editor = Editor(field);
        var chips = ChipsIn(editor);
        Assert.Equal(3, chips.Count);
        var bob = Middle(editor, chips[1]);

        var part = await editor.PartAt(bob);

        Assert.NotNull(part);
        Assert.True(Shows((FrameworkElement)part.Card, "Bob Rus"), "The card doesn't show Bob Rus.");
        Assert.False(Shows((FrameworkElement)part.Card, "Ana Pop"), "The card shows Ana Pop.");
        Assert.True(part.Box.Contains(bob), "The box doesn't hold the chip's middle.");
        Assert.False(part.Box.Contains(Middle(editor, chips[0])), "The box holds the first chip's middle.");
        Assert.False(part.Box.Contains(Middle(editor, chips[2])), "The box holds the last chip's middle.");
    }

    [WpfTheory]
    [InlineData(MarkdownField.Title)]
    [InlineData(MarkdownField.Description)]
    public async Task Resting_on_plain_text_gives_no_card(MarkdownField field)
    {
        var editor = Editor(field);
        var chip = ChipsIn(editor)[0];
        var line = chip.TranslatePoint(new Point(0, chip.ActualHeight / 2), editor.TextArea.TextView).Y;

        var onCall = await editor.PartAt(editor.TextArea.TextView.TranslatePoint(new Point(2, line), editor));

        Assert.Null(onCall);
    }

    [WpfTheory]
    [InlineData(MarkdownField.Title)]
    [InlineData(MarkdownField.Description)]
    public async Task Resting_on_a_chip_whose_contact_Contacts_cannot_find_gives_no_card(MarkdownField field)
    {
        var editor = Editor(field);
        var chips = ChipsIn(editor);

        var onZed = await editor.PartAt(Middle(editor, chips[2]));

        Assert.Null(onZed);
    }
}
