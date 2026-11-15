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
