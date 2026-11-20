using System.Text.RegularExpressions;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Adding images to the Add window's Description, on the common path: Ctrl+V, Ctrl+Shift+I with a chosen file, a dropped
/// file, and an error. How an image is fetched, kept, and linked, and every failure,
/// are in the unit tests of the image insertion.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowImageTests
{
    private static readonly Regex ImageLink = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static byte[] Png() => FakeImageEncoder.Image(ImageFormat.Png, 100, 80);

    private static AddScreen Open(string description = "", int cursor = 0)
    {
        var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, description, cursor);
        return screen;
    }

    private static Match OnlyLink(AddScreen screen) => Assert.Single(ImageLink.Matches(screen.TextOf(EditorFields.Description)));

    [WpfFact]
    public void Ctrl_V_with_an_image_on_the_clipboard_inserts_its_link_at_the_cursor()
    {
        using var screen = Open("ab", 1);
        screen.Services.Clipboard.Image = Png();

        screen.PressCtrl(false, Key.V);

        var link = OnlyLink(screen);
        Assert.Equal("a" + link.Value + "b", screen.TextOf(EditorFields.Description));
        Assert.True(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link.Groups[2].Value)));
    }

    [WpfFact]
    public void Ctrl_Shift_I_inserts_the_chosen_file_with_its_name_as_alt_text()
    {
        using var screen = Open();
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("Cat photo.png", Png());

        screen.PressCtrl(true, Key.I);

        Assert.Equal("Cat photo", OnlyLink(screen).Groups[1].Value);
    }

    [WpfFact]
    public void A_dropped_image_file_is_inserted_into_the_Description()
    {
        using var screen = Open("ab", 1);
        var path = screen.Services.PutImageFile("Cat.png", Png());

        var taken = SimulatedDrop.Files(EditorFields.Field(screen.Window, EditorFields.Description), path);

        Assert.True(taken);
        Assert.Equal("Cat", OnlyLink(screen).Groups[1].Value);
    }

    [WpfFact]
    public void An_image_that_cannot_be_kept_shows_an_error_and_keeps_the_text()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.Clipboard.ImageFailure = new System.Runtime.InteropServices.ExternalException("The clipboard is busy.");

        screen.PressCtrl(false, Key.V);

        Assert.Contains(screen.Texts.Except(before), text => text.Contains("image", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("ab", screen.TextOf(EditorFields.Description));
        Assert.True(screen.IsShowing);
    }
}
