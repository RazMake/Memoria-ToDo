using System.Windows.Automation;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle.TitleScreen;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

/// <summary>How a title shows a mention: one chip, whatever punctuation or styling its text holds. The title is laid out but never shown.</summary>
[Unit]
public class FormattedTitleChipTests
{
    [WpfFact]
    public void Shows_a_mention_whose_text_holds_escaped_punctuation_as_one_chip_with_that_text()
    {
        using var screen = new TitleScreen(inWindow: false);

        screen.Title.Markdown = $"Ask {EscapedMention} now";

        var chip = Assert.Single(screen.Title.Chips());
        Assert.Equal("Ana Pop (SDE)", TitleChips.TextOf(chip));
        Assert.Equal("Ask Ana Pop (SDE) now", screen.Text);
        Assert.Equal("Ask Ana Pop (SDE) now", AutomationProperties.GetName(screen.Title));
    }

    [WpfTheory]
    [InlineData("bold", "[Ana **Pop** Li](contact:a1 \"{fullName}\")")]
    [InlineData("italic", "[Ana *Pop* Li](contact:a1 \"{fullName}\")")]
    public void Shows_a_mention_with_bold_or_italic_on_part_of_its_text_as_one_chip_keeping_that_style_on_that_part(string style, string mention)
    {
        using var screen = new TitleScreen(inWindow: false);

        screen.Title.Markdown = $"Ask {mention} now";

        var chip = Assert.Single(screen.Title.Chips());
        Assert.Equal("Ana Pop Li", TitleChips.TextOf(chip));
        Assert.True(HasStyle(style, screen, "Pop"), $"'Pop' isn't {style}.");
        Assert.False(HasStyle(style, screen, "Ana"), $"'Ana' is {style} too.");
        Assert.False(HasStyle(style, screen, "Li"), $"'Li' is {style} too.");
    }
}
