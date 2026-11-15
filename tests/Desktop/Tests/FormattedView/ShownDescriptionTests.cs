using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

/// <summary>Which page a description shows: Rendering's page for its Markdown in the current theme, and never the same page twice in a row.</summary>
[Unit]
public class ShownDescriptionTests
{
    private readonly FakeTheme _theme = new();
    private readonly List<string> _pages = [];
    private readonly ShownDescription _shown;
    private Uri _clone = new("https://fake.example/clone/");

    public ShownDescriptionTests()
    {
        _shown = new ShownDescription(_theme, () => _clone, _pages.Add);
    }

    private string Page(string markdown, bool dark) => DescriptionPage.Of(markdown, dark, _clone);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_nothing_before_a_Markdown_is_set(bool dark)
    {
        _theme.IsDark = dark;

        _shown.ThemeChanged();

        Assert.Empty(_pages);
    }

    [Fact]
    public void Shows_the_light_page_for_the_Markdown_in_a_light_theme()
    {
        _shown.Set("Some **bold** text");

        Assert.Equal([Page("Some **bold** text", dark: false)], _pages);
    }

    [Fact]
    public void Shows_the_dark_page_when_the_theme_is_dark()
    {
        _theme.IsDark = true;

        _shown.Set("Some **bold** text");

        Assert.Equal([Page("Some **bold** text", dark: true)], _pages);
    }

    [Fact]
    public void Builds_the_page_for_the_clone_address_it_is_given_when_it_shows()
    {
        _clone = new Uri("https://other.fake.example/clone/");

        _shown.Set("![a](images/x/y.png)");

        Assert.Equal([Page("![a](images/x/y.png)", dark: false)], _pages);
    }

    [Fact]
    public void Setting_another_Markdown_shows_its_page_in_place_of_the_old()
    {
        _shown.Set("First");

        _shown.Set("Second");

        Assert.Equal([Page("First", false), Page("Second", false)], _pages);
    }

    [Fact]
    public void Setting_the_same_Markdown_again_in_the_same_theme_shows_nothing_more()
    {
        _shown.Set("Same");

        _shown.Set("Same");

        Assert.Single(_pages);
    }

    [Fact]
    public void Switching_the_theme_shows_the_same_Markdown_in_the_other_theme()
    {
        _shown.Set("Some **bold** text");

        _theme.IsDark = true;
        _shown.ThemeChanged();

        Assert.Equal([Page("Some **bold** text", false), Page("Some **bold** text", true)], _pages);
    }

    [Fact]
    public void A_theme_change_that_leaves_the_theme_as_it_was_shows_nothing_more()
    {
        _shown.Set("Some text");

        _shown.ThemeChanged();

        Assert.Single(_pages);
    }
}
