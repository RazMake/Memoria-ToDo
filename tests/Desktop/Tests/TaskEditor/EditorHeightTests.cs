using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// How tall the Description is. When the window gives the editor a set height, the Description fills what the other
/// parts leave and scrolls inside itself. Add's window grows with its content instead, so there, as in a window sized to
/// its content, the Description grows with its text between 140 and 400 DIP.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class EditorHeightTests
{
    private static readonly string LongText = string.Join("\n\n", Enumerable.Range(1, 200).Select(i => $"line {i}"));

    [WpfFact]
    public void With_a_set_height_the_Description_fills_it_past_400_down_to_the_Tags_row()
    {
        using var host = new EditorHost(height: 900);

        var (description, _) = host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description));
        var (plus, _) = host.PlaceOf(TagControls.Plus(host.Editor));

        Assert.True(description.Height > 400, $"The Description is only {description.Height} tall.");
        Assert.True(plus.Top - description.Bottom < 60, "The Description stops far above the Tags row.");
    }

    [WpfFact]
    public void With_a_set_height_a_long_text_scrolls_inside_the_Description_and_keeps_Save_in_view()
    {
        using var host = new EditorHost(height: 600);
        var empty = host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description)).Place.Height;

        host.Editor.Value = host.Editor.Value with { Description = LongText };
        var (description, editorHeight) = host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description));
        var (save, _) = host.PlaceOf(host.SaveButton);

        Assert.Equal(empty, description.Height, 0.5);
        Assert.True(save.Bottom <= editorHeight, "Save is pushed out of the editor.");
    }

    [WpfFact]
    public void With_a_set_height_a_message_line_makes_the_Description_shorter_not_the_editor_taller()
    {
        using var host = new EditorHost(height: 600);
        var before = host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description)).Place.Height;

        host.ShowMessage("Pick a repository folder.");
        var (description, editorHeight) = host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description));
        var (save, _) = host.PlaceOf(host.SaveButton);

        Assert.True(description.Height < before, "The Description did not give way to the message line.");
        Assert.True(save.Bottom <= editorHeight, "Save is pushed out of the editor.");
    }

    [WpfFact]
    public void With_no_set_height_an_empty_Description_is_140_tall()
    {
        using var host = new EditorHost(height: double.NaN);

        Assert.Equal(140, DescriptionHeight(host), 0.5);
    }

    [WpfFact]
    public void With_no_set_height_a_long_Description_stops_growing_at_400_and_scrolls()
    {
        using var host = new EditorHost(height: double.NaN);
        var few = DescriptionHeight(host);

        host.Editor.Value = host.Editor.Value with { Description = LongText };

        Assert.Equal(400, DescriptionHeight(host), 0.5);
        Assert.True(DescriptionHeight(host) > few);
    }

    private static double DescriptionHeight(EditorHost host) =>
        host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description)).Place.Height;
}
