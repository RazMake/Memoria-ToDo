using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Getting an image from the clipboard, the file dialog, or a dropped file; keeping it through Tasks; and inserting its
/// link. How Tasks stores and shrinks images, and how the link is written, are tested in their modules.
/// </summary>
[Unit]
public class ImageInsertionTests
{
    private static readonly Regex ImageLink = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));

    private readonly EditorServices _services = new();
    private readonly List<string> _reports = [];
    private readonly string _taskId;
    private readonly ImageInsertion _images;

    public ImageInsertionTests()
    {
        _taskId = _services.Tasks.StartDraft().TaskId;
        _images = new ImageInsertion(_services.Tasks, _services.Clipboard, _services.ImageFiles, _services.Files,
            () => _taskId, _reports.Add);
    }

    private static byte[] Png(int size = 0) => FakeImageEncoder.Image(ImageFormat.Png, 100, 80, size);

    private static Match OnlyLink(FakeTextField field) => Assert.Single(ImageLink.Matches(field.Text));

    private IEnumerable<string> ImageFilesKept() =>
        _services.Files.ListFiles(Path.Combine(_services.Device, "images", _taskId));

    private void AssertNothingInserted(FakeTextField field, string text, int cursor)
    {
        Assert.Equal(text, field.Text);
        Assert.Equal(cursor, field.CaretOffset);
        Assert.Empty(ImageFilesKept());
    }

    [Fact]
    public void Pasting_with_no_image_on_the_clipboard_does_nothing_and_says_so()
    {
        var field = new FakeTextField("ab", 1);

        Assert.False(_images.PasteClipboardImage(field));

        AssertNothingInserted(field, "ab", 1);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Pasting_inserts_the_link_at_the_cursor_and_keeps_the_text_around_it()
    {
        var field = new FakeTextField("ab", 1);
        _services.Clipboard.Image = Png();

        Assert.True(_images.PasteClipboardImage(field));

        var link = OnlyLink(field);
        Assert.Equal("a" + link.Value + "b", field.Text);
        Assert.Equal(1 + link.Length, field.CaretOffset);
        Assert.Equal(1, field.FocusCount);
    }

    [Fact]
    public void Pasting_replaces_the_selected_text()
    {
        var field = new FakeTextField("a old b", 2, 3);
        _services.Clipboard.Image = Png();

        _images.PasteClipboardImage(field);

        Assert.Equal("a " + OnlyLink(field).Value + " b", field.Text);
    }

    [Fact]
    public void A_pasted_image_has_the_alt_text_image_and_its_file_is_kept_on_this_device()
    {
        var field = new FakeTextField();
        _services.Clipboard.Image = Png();

        _images.PasteClipboardImage(field);

        var link = OnlyLink(field);
        Assert.Equal("image", link.Groups[1].Value);
        Assert.Matches($"^images/{Regex.Escape(_taskId)}/[^/]+\\.png$", link.Groups[2].Value);
        Assert.True(_services.Files.FileExists(_services.DraftImagePath(link.Groups[2].Value)));
    }

    [Fact]
    public void Two_pasted_images_get_two_different_files()
    {
        var field = new FakeTextField();
        _services.Clipboard.Image = Png();

        _images.PasteClipboardImage(field);
        _images.PasteClipboardImage(field);

        var paths = ImageLink.Matches(field.Text).Select(match => match.Groups[2].Value).ToList();
        Assert.NotEqual(paths[0], paths[1]);
        Assert.Equal(2, ImageFilesKept().Count());
    }

    [Fact]
    public void A_successful_insert_clears_the_message()
    {
        var field = new FakeTextField();
        _services.Clipboard.Image = Png();

        _images.PasteClipboardImage(field);

        Assert.Equal([""], _reports);
    }

    [Fact]
    public void The_clipboard_image_is_used_without_opening_the_file_dialog()
    {
        var field = new FakeTextField();
        _services.Clipboard.Image = Png();
        _services.ImageFiles.Path = _services.PutImageFile("cat.png", Png());

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        Assert.Equal(0, _services.ImageFiles.Opened);
        Assert.Equal("image", OnlyLink(field).Groups[1].Value);
    }

    [Fact]
    public void With_no_clipboard_image_the_file_dialog_opens_once_and_the_chosen_file_is_inserted_with_its_name_as_alt_text()
    {
        var field = new FakeTextField("ab", 2);
        _services.ImageFiles.Path = _services.PutImageFile("Cat photo.png", Png());

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        Assert.Equal(1, _services.ImageFiles.Opened);
        var link = OnlyLink(field);
        Assert.Equal("Cat photo", link.Groups[1].Value);
        Assert.StartsWith("ab", field.Text);
        Assert.True(_services.Files.FileExists(_services.DraftImagePath(link.Groups[2].Value)));
    }

    [Fact]
    public void A_file_name_with_nothing_before_the_extension_gets_the_alt_text_image()
    {
        var field = new FakeTextField();
        _services.ImageFiles.Path = _services.PutImageFile(".png", Png());

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        Assert.StartsWith("![image](", field.Text);
    }

    [Fact]
    public void A_typed_slash_text_is_replaced_by_the_image()
    {
        var field = new FakeTextField("a /im", 5);
        _services.Clipboard.Image = Png();

        _images.InsertClipboardOrChosen(field, Snippets.Image, 2);

        Assert.Equal("a " + OnlyLink(field).Value, field.Text);
    }

    [Fact]
    public void Cancelling_the_file_dialog_inserts_nothing_shows_no_error_and_returns_the_cursor_to_the_field()
    {
        var field = new FakeTextField("ab", 1);
        _services.ImageFiles.Path = null;

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        AssertNothingInserted(field, "ab", 1);
        Assert.Empty(_reports);
        Assert.Equal(1, field.FocusCount);
    }

    [Fact]
    public void The_file_dialog_counts_as_open_only_while_it_is_open()
    {
        var field = new FakeTextField();
        var openDuring = false;
        _services.ImageFiles.WhileOpen = () => openDuring = _images.IsChoosingFile;

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        Assert.True(openDuring);
        Assert.False(_images.IsChoosingFile);
    }

    [Fact]
    public void The_file_dialog_no_longer_counts_as_open_when_it_fails()
    {
        var field = new FakeTextField();
        _services.ImageFiles.WhileOpen = () => throw new InvalidOperationException("boom");

        Assert.Throws<InvalidOperationException>(() => _images.InsertClipboardOrChosen(field, Snippets.Image, null));

        Assert.False(_images.IsChoosingFile);
    }

    [Fact]
    public void A_dropped_file_is_inserted_with_its_name_as_alt_text()
    {
        var field = new FakeTextField("ab", 1);
        var path = _services.PutImageFile("Cat.png", Png());

        _images.InsertFile(field, path);

        var link = OnlyLink(field);
        Assert.Equal("Cat", link.Groups[1].Value);
        Assert.StartsWith("a", field.Text);
        Assert.True(_services.Files.FileExists(_services.DraftImagePath(link.Groups[2].Value)));
    }

    [Fact]
    public void An_image_too_large_to_shrink_reports_that_and_inserts_nothing()
    {
        var field = new FakeTextField("ab", 1);
        _services.Encoder.OutputSize = (_, _, _, _) => 2_000_000;
        _services.Clipboard.Image = Png(2_000_000);

        Assert.True(_images.PasteClipboardImage(field));

        Assert.Equal(["The image wasn't added: it can't be shrunk to 1 MB."], _reports);
        AssertNothingInserted(field, "ab", 1);
        Assert.Equal(1, field.FocusCount);
    }

    [Fact]
    public void A_chosen_file_that_is_not_an_image_reports_it_and_inserts_nothing()
    {
        var field = new FakeTextField("ab", 1);
        _services.ImageFiles.Path = _services.PutImageFile("notes.png", "just words"u8.ToArray());

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        Assert.StartsWith("The image wasn't added: ", Assert.Single(_reports));
        AssertNothingInserted(field, "ab", 1);
    }

    [Fact]
    public void A_chosen_file_that_cannot_be_read_reports_it_and_inserts_nothing()
    {
        var field = new FakeTextField("ab", 1);
        _services.ImageFiles.Path = Path.Combine(Path.GetTempPath(), "editor-tests", "pictures", "missing.png");

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        Assert.StartsWith("The image wasn't added: ", Assert.Single(_reports));
        AssertNothingInserted(field, "ab", 1);
    }

    [Fact]
    public void A_busy_clipboard_reports_it_inserts_nothing_and_counts_as_handled_when_pasting()
    {
        var field = new FakeTextField("ab", 1);
        _services.Clipboard.ImageFailure = new ExternalException("The clipboard is busy.");

        Assert.True(_images.PasteClipboardImage(field));

        Assert.Equal(["The image wasn't added: The clipboard is busy."], _reports);
        AssertNothingInserted(field, "ab", 1);
    }

    [Fact]
    public void A_busy_clipboard_reports_it_and_does_not_fall_back_to_the_file_dialog()
    {
        var field = new FakeTextField("ab", 1);
        _services.Clipboard.ImageFailure = new ExternalException("The clipboard is busy.");

        _images.InsertClipboardOrChosen(field, Snippets.Image, null);

        Assert.Single(_reports);
        Assert.Equal(0, _services.ImageFiles.Opened);
    }

    [Fact]
    public void An_image_that_cannot_be_written_reports_it_and_inserts_nothing()
    {
        var field = new FakeTextField("ab", 1);
        _services.Files.FailWritesUnder(Path.Combine(_services.Device, "images"));
        _services.Clipboard.Image = Png();

        _images.PasteClipboardImage(field);

        Assert.StartsWith("The image wasn't added: ", Assert.Single(_reports));
        Assert.Equal("ab", field.Text);
    }

    [Fact]
    public void A_success_after_a_failure_clears_the_message()
    {
        var field = new FakeTextField();
        _services.Clipboard.ImageFailure = new ExternalException("busy");
        _images.PasteClipboardImage(field);
        _services.Clipboard.ImageFailure = null;
        _services.Clipboard.Image = Png();

        _images.PasteClipboardImage(field);

        Assert.Equal("", _reports[^1]);
    }

    [Fact]
    public void An_insert_is_one_undo_step()
    {
        var field = new FakeTextField("ab", 1);
        _services.Clipboard.Image = Png();
        _images.PasteClipboardImage(field);

        field.Undo();

        Assert.Equal("ab", field.Text);
    }
}
