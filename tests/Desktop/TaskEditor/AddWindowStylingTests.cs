using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Add styles Markdown in place. A test sees styling only as pixels (<see cref="RenderedField"/>): it compares a field that
/// holds styled Markdown with a control that holds the same characters, which Markdown reads as plain text.
/// </summary>
[UX]
public class AddWindowStylingTests
{
    private const string LightRed = "#d1242f";
    private const string DarkRed = "#ff7b72";
    private const string LightGreen = "#1a7f37";
    private const string Bold = "**mmmmmmmmmmmmmmmmmmmm**";
    private const string BoldControl = @"\*\*mmmmmmmmmmmmmmmmmmmm\*\*";
    // Letters with upright stems, so a slant shows clearly; the stars of the control add some slant of their own.
    private const string Italic = "*llllllllllllllllllll*";
    private const string ItalicControl = @"\*llllllllllllllllllll\*";
    private const string Strike = "~~mmmmmmmmmmmmmmmmmmmm~~";
    private const string StrikeControl = @"\~\~mmmmmmmmmmmmmmmmmmmm\~\~";

    [WpfFact]
    public async Task Colored_text_in_the_Description_shows_in_the_palette_color_for_the_light_theme()
    {
        using var screen = new AddScreen();

        var look = await Look(screen, EditorFields.Description, ":red[mmmmmmmmmmmm]");

        Assert.True(look.ShowsColor(LightRed));
        Assert.False(look.ShowsColor(DarkRed));
    }

    [WpfFact]
    public async Task Colored_text_in_the_Title_shows_in_the_palette_color()
    {
        using var screen = new AddScreen();

        var look = await Look(screen, EditorFields.Title, ":green[mmmmmmmmmmmm]");

        Assert.True(look.ShowsColor(LightGreen));
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
    public async Task Text_with_an_unknown_color_name_shows_in_no_palette_color()
    {
        using var screen = new AddScreen();

        var look = await Look(screen, EditorFields.Description, ":teal[mmmmmmmmmmmm]");

        Assert.False(look.ShowsColor(LightRed));
        Assert.False(look.ShowsColor(LightGreen));
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
    public async Task Bold_text_in_the_Title_shows_heavier_than_plain_text()
    {
        using var screen = new AddScreen();

        var styled = await Look(screen, EditorFields.Title, Bold);
        var plain = await Look(screen, EditorFields.Title, BoldControl);

        Assert.True(styled.TotalInk() > plain.TotalInk());
    }

    [WpfFact]
    public async Task Italic_text_in_the_Description_leans_right_where_plain_text_does_not()
    {
        using var screen = new AddScreen();

        var styled = await Look(screen, EditorFields.Description, Italic);
        var plain = await Look(screen, EditorFields.Description, ItalicControl);

        Assert.True(styled.Slant() > plain.Slant() + 0.5);
    }

    [WpfFact]
    public async Task Italic_text_in_the_Title_leans_right_where_plain_text_does_not()
    {
        using var screen = new AddScreen();

        var styled = await Look(screen, EditorFields.Title, Italic);
        var plain = await Look(screen, EditorFields.Title, ItalicControl);

        Assert.True(styled.Slant() > plain.Slant() + 0.5);
    }

    [WpfFact]
    public async Task Struck_through_text_shows_more_ink_than_plain_text()
    {
        using var screen = new AddScreen();

        var styled = await Look(screen, EditorFields.Description, Strike);
        var plain = await Look(screen, EditorFields.Description, StrikeControl);

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
    public async Task Markdown_characters_show_dimmed()
    {
        using var screen = new AddScreen();

        // Each "\*" is a marker followed by a plain star; each "\a" is plain text. Both hold the same characters.
        var markers = await Look(screen, EditorFields.Description, string.Concat(Enumerable.Repeat(@"\*", 30)) + new string('a', 30));
        var plain = await Look(screen, EditorFields.Description, string.Concat(Enumerable.Repeat(@"\a", 30)) + new string('*', 30));

        Assert.True(markers.TotalInk() < plain.TotalInk() * 0.97);
    }

    [WpfFact]
    public async Task Typed_text_shows_at_once_and_its_styling_follows()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Focus(EditorFields.Title);

        EditorFields.Type(screen.Window, EditorFields.Description, ":red[mmmmmmmmmmmm]");

        Assert.Equal(":red[mmmmmmmmmmmm]", EditorFields.TextOf(screen.Window, EditorFields.Description));
        await RenderedField.Settled();
        Assert.True(RenderedField.Of(screen.Window, EditorFields.Description).ShowsColor(LightRed));
    }

    [WpfFact]
    public async Task Styling_follows_when_the_text_changes_again()
    {
        using var screen = new AddScreen();
        await Look(screen, EditorFields.Description, ":red[mmmmmmmmmmmm]");

        EditorFields.Type(screen.Window, EditorFields.Description, ":green[mmmmmmmmmmmm]");
        await RenderedField.Settled();
        var look = RenderedField.Of(screen.Window, EditorFields.Description);

        Assert.True(look.ShowsColor(LightGreen));
        Assert.False(look.ShowsColor(LightRed));
    }

    [WpfFact]
    public void Add_shows_no_web_view()
    {
        using var screen = new AddScreen();
        screen.Show();

        var names = VisualTypeNames(screen.Window);

        Assert.DoesNotContain(names, name => name.Contains("WebView", StringComparison.OrdinalIgnoreCase));
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

    private static IEnumerable<string> VisualTypeNames(DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child.GetType().Name;
            foreach (var name in VisualTypeNames(child))
            {
                yield return name;
            }
        }
    }
}
