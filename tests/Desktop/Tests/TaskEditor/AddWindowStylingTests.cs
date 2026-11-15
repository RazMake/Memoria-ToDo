using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Add styles Markdown in place. A test sees styling only as pixels (<see cref="RenderedField"/>): it compares a field that
/// holds styled Markdown with a control that holds the same characters, which Markdown reads as plain text. Which spans a
/// text has, and their styles, are Rendering's, tested there; these check that the field paints them.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowStylingTests
{
    private const string LightRed = "#d1242f";
    private const string DarkRed = "#ff7b72";
    private const string Bold = "**mmmmmmmmmmmmmmmmmmmm**";
    private const string BoldControl = "** mmmmmmmmmmmmmmmmmmmm**";

    [WpfFact]
    public async Task Colored_text_in_the_Description_shows_in_the_palette_color_for_the_light_theme()
    {
        using var screen = new AddScreen();

        var look = await Look(screen, EditorFields.Description, ":red[mmmmmmmmmmmm]");

        Assert.True(look.ShowsColor(LightRed));
        Assert.False(look.ShowsColor(DarkRed));
    }

    [WpfFact]
    public async Task Colored_text_shows_in_the_dark_value_while_the_theme_is_dark()
    {
        using var screen = new AddScreen();
        screen.Services.Theme.IsDark = true;

        var look = await Look(screen, EditorFields.Description, ":red[mmmmmmmmmmmm]");

        Assert.True(look.ShowsColor(DarkRed));
        Assert.False(look.ShowsColor(LightRed));
    }

    [WpfFact]
    public async Task Colored_text_changes_to_the_other_value_when_the_theme_changes()
    {
        using var screen = new AddScreen();
        await Look(screen, EditorFields.Description, ":red[mmmmmmmmmmmm]");

        screen.Services.Theme.Switch(true);
        await RenderedField.Settled();
        var look = RenderedField.Of(screen.Window, EditorFields.Description);

        Assert.True(look.ShowsColor(DarkRed));
        Assert.False(look.ShowsColor(LightRed));
    }

    [WpfFact]
    public async Task Bold_text_in_the_Description_shows_heavier_than_plain_text()
    {
        using var screen = new AddScreen();

        var styled = await Look(screen, EditorFields.Description, Bold);
        var plain = await Look(screen, EditorFields.Description, BoldControl);

        Assert.True(styled.TotalInk() > plain.TotalInk());
    }

    [WpfFact]
    public async Task A_heading_in_the_Description_shows_taller_than_plain_text()
    {
        using var screen = new AddScreen();

        var heading = await Look(screen, EditorFields.Description, "# mmmmmmmmmm");
        var plain = await Look(screen, EditorFields.Description, "#mmmmmmmmmm");

        Assert.True(heading.InkHeight() > plain.InkHeight());
    }

    [WpfTheory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task A_heading_of_level_1_to_3_shows_in_its_color_for_the_theme(bool dark, int level)
    {
        using var screen = new AddScreen();
        screen.Services.Theme.IsDark = dark;

        var look = await Look(screen, EditorFields.Description, new string('#', level) + " mmmmmmmmmmmm");

        Assert.True(look.ShowsColor(TextLooks.Of(dark).HeadingColor(level)!));
        Assert.False(look.ShowsColor(TextLooks.Of(!dark).HeadingColor(level)!));
    }

    [WpfTheory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task A_heading_of_level_4_to_6_shows_in_none_of_the_heading_colors(int level)
    {
        using var screen = new AddScreen();

        var look = await Look(screen, EditorFields.Description, new string('#', level) + " mmmmmmmmmmmm");

        Assert.False(look.ShowsColor(TextLooks.Light.Heading1));
        Assert.False(look.ShowsColor(TextLooks.Light.Heading2));
        Assert.False(look.ShowsColor(TextLooks.Light.Heading3));
    }

    [WpfFact]
    public async Task A_heading_shows_at_the_size_of_its_level_and_a_level_4_heading_is_taller_than_plain_text()
    {
        using var screen = new AddScreen();

        var one = await Look(screen, EditorFields.Description, "# mmmmmmmmmm");
        var two = await Look(screen, EditorFields.Description, "## mmmmmmmmmm");
        var four = await Look(screen, EditorFields.Description, "#### mmmmmmmmmm");
        var plain = await Look(screen, EditorFields.Description, "####mmmmmmmmmm");

        Assert.True(one.InkHeight() > two.InkHeight());
        Assert.True(two.InkHeight() > four.InkHeight());
        Assert.True(four.InkHeight() > plain.InkHeight());
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_link_shows_in_the_link_color_for_the_theme(bool dark)
    {
        using var screen = new AddScreen();
        screen.Services.Theme.IsDark = dark;

        var look = await Look(screen, EditorFields.Description, "[mmmmmmmmmmmm](https://example.com)");

        Assert.True(look.ShowsColor(TextLooks.Of(dark).Link));
        Assert.False(look.ShowsColor(TextLooks.Of(!dark).Link, exact: true));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Code_shows_in_the_code_color_for_the_theme(bool dark)
    {
        using var screen = new AddScreen();
        screen.Services.Theme.IsDark = dark;

        var look = await Look(screen, EditorFields.Description, "`mmmmmmmmmmmm`");

        Assert.True(look.ShowsColor(TextLooks.Of(dark).Code));
        Assert.False(look.ShowsColor(TextLooks.Of(!dark).Code));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Code_shows_on_the_faded_gray_background_of_the_look(bool dark)
    {
        using var screen = new AddScreen();
        screen.Services.Theme.IsDark = dark;
        var code = TextLooks.Of(dark);

        var look = await Look(screen, EditorFields.Description, "`mmmmmmmmmmmm`");
        var plain = await Look(screen, EditorFields.Description, "'mmmmmmmmmmmm'");

        Assert.True(look.ShowsBackdrop(code.CodeBackground, code.CodeBackgroundOpacity));
        Assert.False(plain.ShowsBackdrop(code.CodeBackground, code.CodeBackgroundOpacity));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Italic_text_shows_in_the_italic_color_for_the_theme(bool dark)
    {
        using var screen = new AddScreen();
        screen.Services.Theme.IsDark = dark;

        var look = await Look(screen, EditorFields.Description, "_mmmmmmmmmmmm_");

        Assert.True(look.ShowsColor(TextLooks.Of(dark).Italic));
        Assert.False(look.ShowsColor(TextLooks.Of(!dark).Italic));
    }

    [WpfFact]
    public async Task Palette_color_wins_over_the_italic_color_on_the_same_text()
    {
        using var screen = new AddScreen();

        var look = await Look(screen, EditorFields.Description, ":red[_mmmmmmmmmmmm_]");

        Assert.True(look.ShowsColor(LightRed));
        Assert.False(look.ShowsColor(TextLooks.Light.Italic));
    }

    [WpfFact]
    public async Task Heading_link_code_and_italic_colors_change_when_the_theme_changes()
    {
        using var screen = new AddScreen();
        var text = "# mmmmmmmm\n[mmmmmmmm](https://example.com)\n`mmmmmmmm`\n_mmmmmmmm_";
        var light = await Look(screen, EditorFields.Description, text);
        Assert.True(light.ShowsColor(TextLooks.Light.Heading1));

        screen.Services.Theme.Switch(true);
        await RenderedField.Settled();
        var dark = RenderedField.Of(screen.Window, EditorFields.Description);

        Assert.True(dark.ShowsColor(TextLooks.Dark.Heading1));
        Assert.True(dark.ShowsColor(TextLooks.Dark.Link));
        Assert.True(dark.ShowsColor(TextLooks.Dark.Code));
        Assert.True(dark.ShowsColor(TextLooks.Dark.Italic));
        Assert.False(dark.ShowsColor(TextLooks.Light.Heading1));
        Assert.False(dark.ShowsColor(TextLooks.Light.Link));
        Assert.False(dark.ShowsColor(TextLooks.Light.Code));
        Assert.False(dark.ShowsColor(TextLooks.Light.Italic));
    }

    [WpfFact]
    public async Task A_hash_in_the_Title_shows_as_typed_and_not_as_a_heading()
    {
        using var screen = new AddScreen();

        var hash = await Look(screen, EditorFields.Title, "# mmmmmmmmmm");
        var plain = await Look(screen, EditorFields.Title, "#mmmmmmmmmm");

        Assert.Equal(plain.InkHeight(), hash.InkHeight());
    }

    [WpfFact]
    public async Task Typed_text_shows_at_once_and_its_styling_follows()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Focus(EditorFields.Title);
        await RenderedField.Settled();

        EditorFields.Type(screen.Window, EditorFields.Description, ":red[mmmmmmmmmmmm]");

        Assert.Equal(":red[mmmmmmmmmmmm]", EditorFields.TextOf(screen.Window, EditorFields.Description));
        await RenderedField.Settled();
        Assert.True(RenderedField.Of(screen.Window, EditorFields.Description).ShowsColor(LightRed));
    }

    /// <summary>Shows Add, puts the text in the field, lets styling follow, and takes a picture of the field with no caret in it.</summary>
    private static async Task<RenderedField> Look(AddScreen screen, string field, string text)
    {
        screen.Show();
        screen.Focus(field == EditorFields.Title ? EditorFields.Description : EditorFields.Title);
        screen.Editor.Value = screen.Editor.Value with
        {
            Title = field == EditorFields.Title ? text : "",
            Description = field == EditorFields.Description ? text : "",
        };
        await RenderedField.Settled();
        return RenderedField.Of(screen.Window, field);
    }
}
