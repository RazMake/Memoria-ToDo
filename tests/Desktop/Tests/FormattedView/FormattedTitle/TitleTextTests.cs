using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

/// <summary>The text a title shows, and the name assistive tools read for it: the Markdown without its markers and link addresses.</summary>
[Unit]
public class TitleTextTests
{
    [Fact]
    public void Shows_plain_text_as_it_is()
    {
        Assert.Equal("Buy milk", TitleText.Shown("Buy milk"));
    }

    [Fact]
    public void Shows_nothing_for_an_empty_title()
    {
        Assert.Equal("", TitleText.Shown(""));
    }

    [Theory]
    [InlineData("Buy **fresh** milk")]
    [InlineData("Buy *fresh* milk")]
    [InlineData("Buy ~~fresh~~ milk")]
    [InlineData("Buy `fresh` milk")]
    [InlineData("Buy :red[fresh] milk")]
    public void Shows_styled_text_without_its_markers(string markdown)
    {
        Assert.Equal("Buy fresh milk", TitleText.Shown(markdown));
    }

    [Theory]
    [InlineData("See [the docs](https://example.com/docs)", "See the docs")]
    [InlineData("[the docs](https://example.com/docs) now", "the docs now")]
    [InlineData("[the docs](https://example.com/docs)", "the docs")]
    public void Shows_a_link_as_its_text_without_the_address_at_the_end_the_start_or_alone(string markdown, string shown)
    {
        Assert.Equal(shown, TitleText.Shown(markdown));
    }

    [Fact]
    public void Shows_every_kind_of_styling_in_one_title_as_plain_text()
    {
        Assert.Equal(
            "Buy fresh milk today at the shop",
            TitleText.Shown("Buy **fresh** `milk` :red[today] at [the shop](https://example.com/shop)"));
    }
}
