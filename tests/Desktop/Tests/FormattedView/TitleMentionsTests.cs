using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

/// <summary>
/// Which mention a place in a title's shown text is on. The place is an offset into <see cref="TitleText.Shown"/>, which is what
/// the title's text runs hold, so the answer doesn't depend on the markers and link addresses the Markdown also holds.
/// </summary>
[Unit]
public class TitleMentionsTests
{
    private const string Ana = "[Ana](contact:a1 \"{firstName}\")";
    private const string Bo = "[Bo Li](contact:b2 \"{fullName}\")";

    [Fact]
    public void Finds_the_mention_at_an_offset_inside_its_text_with_its_address_and_template()
    {
        var found = TitleMentions.At($"Ask {Ana} now", shownOffset: 5);

        Assert.NotNull(found);
        Assert.Equal("contact:a1", found.Mention.Address);
        Assert.Equal("{firstName}", found.Mention.Template);
        Assert.Equal(4, found.Mention.Start);
        Assert.Equal(Ana.Length, found.Mention.Length);
    }

    [Fact]
    public void Tells_where_the_mention_s_text_sits_in_the_shown_text()
    {
        var found = TitleMentions.At($"Ask {Ana} now", shownOffset: 5);

        Assert.NotNull(found);
        Assert.Equal(4, found.ShownStart);
        Assert.Equal(3, found.ShownLength);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Finds_the_mention_on_each_character_of_its_text(int offset)
    {
        Assert.NotNull(TitleMentions.At($"Ask {Ana} now", offset));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Finds_no_mention_in_the_text_before_it(int offset)
    {
        Assert.Null(TitleMentions.At($"Ask {Ana} now", offset));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(10)]
    public void Finds_no_mention_in_the_text_after_it_the_first_offset_past_its_text_included(int offset)
    {
        Assert.Null(TitleMentions.At($"Ask {Ana} now", offset));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(500)]
    public void Finds_no_mention_at_an_offset_outside_the_shown_text(int offset)
    {
        Assert.Null(TitleMentions.At($"Ask {Ana} now", offset));
    }

    [Fact]
    public void Finds_no_mention_in_a_title_without_one()
    {
        Assert.Null(TitleMentions.At("Buy milk", shownOffset: 2));
    }

    [Fact]
    public void Finds_no_mention_in_an_empty_title()
    {
        Assert.Null(TitleMentions.At("", shownOffset: 0));
    }

    [Fact]
    public void Finds_each_of_two_mentions_by_its_own_text()
    {
        var markdown = $"{Ana} and {Bo}";

        Assert.Equal("contact:a1", TitleMentions.At(markdown, shownOffset: 1)!.Mention.Address);
        Assert.Equal("contact:b2", TitleMentions.At(markdown, shownOffset: 9)!.Mention.Address);
        Assert.Null(TitleMentions.At(markdown, shownOffset: 5));
    }

    [Fact]
    public void Counts_offsets_in_the_shown_text_not_in_the_Markdown()
    {
        // Shown: "Buy fresh Ana". The "**" markers before the mention aren't shown, so it sits 4 characters earlier than in the Markdown.
        var markdown = $"Buy **fresh** {Ana}";

        var found = TitleMentions.At(markdown, shownOffset: 11);

        Assert.NotNull(found);
        Assert.Equal("contact:a1", found.Mention.Address);
        Assert.Equal(10, found.ShownStart);
        Assert.Null(TitleMentions.At(markdown, shownOffset: 6));
    }

    [Fact]
    public void Finds_no_mention_on_a_web_link_beside_a_mention()
    {
        var markdown = $"{Ana} and [the docs](https://example.com/docs)";

        Assert.Null(TitleMentions.At(markdown, shownOffset: 9));
    }

    [Fact]
    public void Finds_no_mention_in_a_name_that_is_plain_text_as_in_a_task_reference_title()
    {
        // What a task reference's title holds: its mentions became plain text, so there is nothing to rest on.
        Assert.Null(TitleMentions.At("Ask Ana now", shownOffset: 5));
    }
}
