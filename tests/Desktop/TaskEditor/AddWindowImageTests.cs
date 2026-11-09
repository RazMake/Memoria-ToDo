using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Adding images to the Add window's Description: Ctrl+V pastes the clipboard's image, Ctrl+Shift+I inserts the clipboard's
/// image or else the file the user chooses, a dropped image file is inserted, and the picker's Image entry does what
/// Ctrl+Shift+I does. Each image is saved through Tasks as a draft image, and the Description gets its link. The tests
/// read the link from the Description and look for its file under the device's draft folder, or the clone after a save.
/// The fake image tools make "images" that carry their own format and size. An error shows as a new text that mentions
/// the image, and leaves the Description and the cursor as they were.
/// </summary>
[UX]
public class AddWindowImageTests
{
    private static readonly Regex ImageLink = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static byte[] Png(int size = 0) => FakeImageEncoder.Image(ImageFormat.Png, 100, 80, size);

    private static byte[] Jpeg() => FakeImageEncoder.Image(ImageFormat.Jpeg, 100, 80);

    private static MatchCollection Links(string description) => ImageLink.Matches(description);

    private static Match OnlyLink(AddScreen screen) => Assert.Single(Links(screen.TextOf(EditorFields.Description)));

    private static AddScreen Open(string description = "", int cursor = 0, int selected = 0)
    {
        var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, description, cursor, selected);
        return screen;
    }

    private static void Paste(AddScreen screen) => screen.PressCtrl(false, Key.V);

    private static void InsertImage(AddScreen screen) => screen.PressCtrl(true, Key.I);

    private static void AssertErrorShownAndTextKept(AddScreen screen, IReadOnlyList<string> textsBefore, string description, int cursor)
    {
        Assert.Contains(screen.Texts.Except(textsBefore), text => text.Contains("image", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(description, screen.TextOf(EditorFields.Description));
        Assert.Equal((cursor, 0), screen.SelectionOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.True(screen.IsShowing);
    }

    private static IEnumerable<string> ImageFilesKept(AddScreen screen) =>
        screen.Services.Files.ListFiles(Path.Combine(screen.Services.Device, "images", screen.Editor.Value.TaskId));

    [WpfFact]
    public void Add_shows_no_line_about_features_not_built_yet()
    {
        using var screen = new AddScreen();
        screen.Show();

        Assert.DoesNotContain(screen.Texts, text => text.StartsWith("Not built yet", StringComparison.OrdinalIgnoreCase));
    }

    [WpfFact]
    public void Ctrl_V_with_an_image_on_the_clipboard_inserts_its_link_at_the_cursor_and_keeps_the_text_around_it()
    {
        using var screen = Open("ab", 1);
        screen.Services.Clipboard.Image = Png();

        Paste(screen);

        var link = OnlyLink(screen);
        Assert.Equal("a" + link.Value + "b", screen.TextOf(EditorFields.Description));
        Assert.Equal((1 + link.Length, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_V_replaces_the_selected_text_with_the_image()
    {
        using var screen = Open("a old b", 2, 3);
        screen.Services.Clipboard.Image = Png();

        Paste(screen);

        var link = OnlyLink(screen);
        Assert.Equal("a " + link.Value + " b", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_pasted_image_has_the_alt_text_image_and_a_link_into_the_tasks_image_folder_with_its_file_kept_on_this_device()
    {
        using var screen = Open();
        screen.Services.Clipboard.Image = Png();

        Paste(screen);

        var link = OnlyLink(screen);
        Assert.Equal("image", link.Groups[1].Value);
        Assert.Matches($"^images/{Regex.Escape(screen.Editor.Value.TaskId)}/[^/]+\\.png$", link.Groups[2].Value);
        Assert.True(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link.Groups[2].Value)));
    }

    [WpfFact]
    public void Two_pasted_images_get_two_different_files()
    {
        using var screen = Open();
        screen.Services.Clipboard.Image = Png();

        Paste(screen);
        Paste(screen);

        var paths = Links(screen.TextOf(EditorFields.Description)).Select(match => match.Groups[2].Value).ToList();
        Assert.Equal(2, paths.Count);
        Assert.NotEqual(paths[0], paths[1]);
        Assert.Equal(2, ImageFilesKept(screen).Count());
    }

    [WpfFact]
    public void An_image_over_one_MiB_is_shrunk_before_it_is_kept()
    {
        using var screen = Open();
        screen.Services.Encoder.OutputSize = (_, width, _, _) => width * 500;
        screen.Services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 4000, 3000, 3_000_000);

        Paste(screen);

        var kept = screen.Services.Files.ReadFile(screen.Services.DraftImagePath(OnlyLink(screen).Groups[2].Value));
        Assert.InRange(kept.Length, 1, 1_048_576);
        Assert.NotEmpty(screen.Services.Encoder.Calls);
    }

    [WpfFact]
    public void Ctrl_Shift_I_inserts_the_clipboards_image_without_opening_the_file_dialog()
    {
        using var screen = Open("x", 1);
        screen.Services.Clipboard.Image = Png();
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("cat.png", Png());

        InsertImage(screen);

        Assert.Equal(0, screen.Services.ImageFiles.Opened);
        Assert.Equal("image", OnlyLink(screen).Groups[1].Value);
    }

    [WpfFact]
    public void Ctrl_Shift_I_without_a_clipboard_image_opens_the_file_dialog_once()
    {
        using var screen = Open();
        screen.Services.Clipboard.Image = null;

        InsertImage(screen);

        Assert.Equal(1, screen.Services.ImageFiles.Opened);
    }

    [WpfFact]
    public void Ctrl_Shift_I_inserts_the_chosen_file_with_its_name_without_extension_as_alt_text()
    {
        using var screen = Open("ab", 2);
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("Cat photo.png", Png());

        InsertImage(screen);

        var link = OnlyLink(screen);
        Assert.Equal("Cat photo", link.Groups[1].Value);
        Assert.StartsWith("ab", screen.TextOf(EditorFields.Description));
        Assert.True(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link.Groups[2].Value)));
    }

    [WpfFact]
    public void A_chosen_JPEG_file_keeps_its_jpg_extension()
    {
        using var screen = Open();
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("cat.jpg", Jpeg());

        InsertImage(screen);

        Assert.EndsWith(".jpg", OnlyLink(screen).Groups[2].Value);
    }

    [WpfFact]
    public void Brackets_in_a_chosen_file_name_are_escaped_in_the_alt_text()
    {
        using var screen = Open();
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("a[1].png", Png());

        InsertImage(screen);

        Assert.StartsWith(@"![a\[1\]](", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_file_name_with_no_name_before_the_extension_gets_the_alt_text_image()
    {
        using var screen = Open();
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile(".png", Png());

        InsertImage(screen);

        Assert.StartsWith("![image](", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Cancelling_the_file_dialog_inserts_nothing_and_shows_no_error()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.ImageFiles.Path = null;

        InsertImage(screen);

        Assert.Equal("ab", screen.TextOf(EditorFields.Description));
        Assert.Equal((1, 0), screen.SelectionOf(EditorFields.Description));
        Assert.Equal(before, screen.Texts);
        Assert.Empty(ImageFilesKept(screen));
    }

    [WpfFact]
    public void Ctrl_Shift_I_in_the_Title_does_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "Buy", 3);
        screen.Services.Clipboard.Image = Png();
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("cat.png", Png());

        InsertImage(screen);

        Assert.Equal("Buy", screen.TextOf(EditorFields.Title));
        Assert.Equal(0, screen.Services.ImageFiles.Opened);
        Assert.Empty(screen.Services.Encoder.Calls);
        Assert.Empty(ImageFilesKept(screen));
    }

    [WpfFact]
    public void A_dropped_image_file_is_inserted_with_its_name_without_extension_as_alt_text()
    {
        using var screen = Open("ab", 1);
        var path = screen.Services.PutImageFile("Cat.png", Png());

        var taken = SimulatedDrop.Files(EditorFields.Field(screen.Window, EditorFields.Description), path);

        Assert.True(taken);
        var link = OnlyLink(screen);
        Assert.Equal("Cat", link.Groups[1].Value);
        Assert.True(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link.Groups[2].Value)));
    }

    [WpfFact]
    public void A_file_dropped_on_the_Title_inserts_nothing()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "Buy", 3);
        var path = screen.Services.PutImageFile("Cat.png", Png());

        SimulatedDrop.Files(EditorFields.Field(screen.Window, EditorFields.Title), path);

        Assert.Equal("Buy", screen.TextOf(EditorFields.Title));
        Assert.Equal("", screen.TextOf(EditorFields.Description));
        Assert.Empty(ImageFilesKept(screen));
    }

    [WpfFact]
    public void An_image_too_large_to_shrink_shows_an_error_and_keeps_the_text()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.Encoder.OutputSize = (_, _, _, _) => 2_000_000;
        screen.Services.Clipboard.Image = Png(2_000_000);

        Paste(screen);

        AssertErrorShownAndTextKept(screen, before, "ab", 1);
        Assert.Empty(ImageFilesKept(screen));
    }

    [WpfFact]
    public void A_chosen_file_that_is_not_an_image_shows_an_error_and_keeps_the_text()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("notes.png", "just words"u8.ToArray());

        InsertImage(screen);

        AssertErrorShownAndTextKept(screen, before, "ab", 1);
        Assert.Empty(ImageFilesKept(screen));
    }

    [WpfFact]
    public void A_chosen_file_that_cannot_be_read_shows_an_error_and_keeps_the_text()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.ImageFiles.Path = Path.Combine(Path.GetTempPath(), "editor-tests", "pictures", "missing.png");

        InsertImage(screen);

        AssertErrorShownAndTextKept(screen, before, "ab", 1);
    }

    [WpfFact]
    public void A_clipboard_that_fails_to_give_its_image_shows_an_error_and_keeps_the_text_when_pasting()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.Clipboard.ImageFailure = new System.Runtime.InteropServices.ExternalException("The clipboard is busy.");

        Paste(screen);

        AssertErrorShownAndTextKept(screen, before, "ab", 1);
    }

    [WpfFact]
    public void A_clipboard_that_fails_to_give_its_image_shows_an_error_and_keeps_the_text_when_inserting_an_image()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.Clipboard.ImageFailure = new System.Runtime.InteropServices.ExternalException("The clipboard is busy.");

        InsertImage(screen);

        AssertErrorShownAndTextKept(screen, before, "ab", 1);
        Assert.Equal(0, screen.Services.ImageFiles.Opened);
    }

    [WpfFact]
    public void An_image_that_cannot_be_written_shows_an_error_and_keeps_the_text()
    {
        using var screen = Open("ab", 1);
        var before = screen.Texts;
        screen.Services.Files.FailWritesUnder(System.IO.Path.Combine(screen.Services.Device, "images"));
        screen.Services.Clipboard.Image = Png();

        Paste(screen);

        AssertErrorShownAndTextKept(screen, before, "ab", 1);
    }

    [WpfFact]
    public void Esc_keeps_the_draft_with_its_image_link_and_the_image_file()
    {
        using var screen = Open();
        screen.Services.Clipboard.Image = Png();
        Paste(screen);
        var link = OnlyLink(screen);

        screen.PressEscape();

        Assert.Equal(link.Value, screen.KeptDraft?.Description);
        Assert.True(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link.Groups[2].Value)));
    }

    [WpfFact]
    public void Showing_Add_again_restores_the_description_with_its_image_link()
    {
        using var screen = Open();
        screen.Services.Clipboard.Image = Png();
        Paste(screen);
        var link = OnlyLink(screen).Value;
        screen.PressEscape();

        screen.Show();

        Assert.Equal(link, screen.Editor.Value.Description);
    }

    [WpfFact]
    public void Saving_moves_the_image_into_the_clone_and_the_task_keeps_the_link()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Cat");
        screen.Edit(EditorFields.Description, "", 0);
        screen.Services.Clipboard.Image = Png();
        Paste(screen);
        var link = OnlyLink(screen).Groups[2].Value;

        screen.Save();

        var task = Assert.Single(screen.Saved);
        Assert.Contains(link, task.Description);
        Assert.True(screen.Services.Files.FileExists(screen.Services.CloneImagePath(link)));
        Assert.False(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link)));
    }

    [WpfFact]
    public void Clearing_the_draft_deletes_its_images()
    {
        using var screen = Open();
        screen.Services.Clipboard.Image = Png();
        Paste(screen);
        var link = OnlyLink(screen).Groups[2].Value;

        screen.Clear();

        Assert.False(screen.Services.Files.FileExists(screen.Services.DraftImagePath(link)));
        Assert.Null(screen.KeptDraft);
    }

    [WpfFact]
    public void Another_window_becoming_active_while_the_file_dialog_is_open_does_not_hide_Add_or_keep_a_draft()
    {
        using var screen = Open("ab", 1);
        var window = screen.Window;
        var dialog = new Window { Width = 100, Height = 100, Owner = window };
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("cat.png", Png());
        screen.Services.ImageFiles.WhileOpen = () =>
        {
            dialog.Show();
            dialog.Activate();
        };

        InsertImage(screen);
        dialog.Close();

        Assert.True(window.IsVisible);
        Assert.Null(screen.KeptDraft);
        var text = screen.TextOf(EditorFields.Description);
        Assert.StartsWith("a", text);
        Assert.EndsWith("b", text);
    }

    [WpfFact]
    public void Choosing_Image_in_the_snippet_list_inserts_the_clipboards_image_in_place_of_the_typed_slash_text()
    {
        using var screen = Open("a ", 2);
        screen.Services.Clipboard.Image = Png();
        screen.Type("/im");

        screen.ClickSnippet("Image");

        var link = OnlyLink(screen);
        Assert.Equal("a " + link.Value, screen.TextOf(EditorFields.Description));
        Assert.Empty(screen.ShownSnippets);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Choosing_Image_in_the_snippet_list_without_a_clipboard_image_inserts_the_chosen_file()
    {
        using var screen = Open();
        screen.Services.ImageFiles.Path = screen.Services.PutImageFile("Cat.png", Png());
        screen.Type("/");

        screen.ClickSnippet("Image");

        Assert.Equal(1, screen.Services.ImageFiles.Opened);
        Assert.Equal("Cat", OnlyLink(screen).Groups[1].Value);
    }

    [WpfFact]
    public void Typing_slash_im_narrows_the_snippet_list_to_Image()
    {
        using var screen = Open();

        screen.Type("/im");

        Assert.Equal(["Image"], screen.ShownSnippets);
    }
}
