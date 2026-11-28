using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Which mentions of a field show as chips, and so show a contact card while the pointer rests on them: a mention whose Markdown
/// spans a line break shows as its Markdown and is no chip. What counts as a mention is Rendering's; the tests give the text literally.
/// </summary>
[Unit]
public class MentionsInTextTests
{
    private const string Ana = "[Ana Pop](contact:ana \"{fullName}\")";

    [Theory]
    [InlineData(MarkdownField.Title)]
    [InlineData(MarkdownField.Description)]
    public void A_mention_on_one_line_is_a_chip(MarkdownField field)
    {
        var chips = new MentionsInText(new TextDocument($"a {Ana} b"), field).Chips;

        var chip = Assert.Single(chips);
        Assert.Equal("contact:ana", chip.Address);
        Assert.Equal(2, chip.Start);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void A_mention_that_spans_a_line_break_is_no_chip(string lineBreak)
    {
        var text = $"a [Ana{lineBreak}Pop](contact:ana \"{{fullName}}\") b";

        var chips = new MentionsInText(new TextDocument(text), MarkdownField.Description).Chips;

        Assert.Empty(chips);
    }

    [Fact]
    public void A_mention_on_one_line_beside_one_across_a_line_break_is_still_a_chip()
    {
        var text = $"{Ana}\n[Bo\nLi](contact:bo \"{{fullName}}\")";

        var chips = new MentionsInText(new TextDocument(text), MarkdownField.Description).Chips;

        Assert.Equal("contact:ana", Assert.Single(chips).Address);
    }

    [Fact]
    public void The_chips_follow_the_text_after_it_changes()
    {
        var document = new TextDocument("none");
        var mentions = new MentionsInText(document, MarkdownField.Description);
        Assert.Empty(mentions.Chips);

        document.Text = Ana;

        Assert.Single(mentions.Chips);
    }
}
