using System.Text.RegularExpressions;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// Adding an image to an existing task, as an assistant does: shrunk by the rules for any pasted image, written straight into
/// the task's folder in the clone, and never a touch on the task's file, an open edit's images, or the draft.
/// </summary>
[Unit]
public sealed class AddImageTests
{
    private const int OneMegabyte = 1_048_576;
    private const int TooBig = OneMegabyte + 1;
    private static readonly Regex Markdown = new(@"^!\[(?<alt>.*)\]\(images/(?<task>[0-9a-f-]{36})/(?<image>[0-9a-f-]{36})\.(?<ext>png|jpg)\)$");

    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(7);

    public AddImageTests() => f.SeedAndUse(id, "Existing");

    private string Add(byte[] image, string alt = "pic") => f.Tasks.AddImage(id, image, alt);

    /// <summary>The file in the clone that the Markdown names.</summary>
    private string CloneFile(string markdown)
    {
        var m = Markdown.Match(markdown);
        Assert.True(m.Success, markdown);
        return Path.Combine(f.CloneImages(id), m.Groups["image"].Value + "." + m.Groups["ext"].Value);
    }

    private byte[] SavedBytes(string markdown) => f.Files.ReadFile(CloneFile(markdown));

    private static string Ext(string markdown) => Markdown.Match(markdown).Groups["ext"].Value;

    private (Func<int> Saved, Func<int> Changed) CountNotices()
    {
        var saved = 0;
        var changed = 0;
        f.Repository.LocalSaved += (_, _) => saved++;
        f.Tasks.Changed += (_, _) => changed++;
        return (() => saved, () => changed);
    }

    // What it writes and returns

    [Fact]
    public void Returns_markdown_pointing_at_images_task_id_image_id_and_extension()
    {
        var markdown = Add(FakeImageEncoder.Image(ImageFormat.Png, 100, 50, 500), "A cat");

        var m = Markdown.Match(markdown);
        Assert.True(m.Success, markdown);
        Assert.Equal("A cat", m.Groups["alt"].Value);
        Assert.Equal(id, m.Groups["task"].Value);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", m.Groups["image"].Value);
    }

    [Fact]
    public void Writes_the_image_into_the_tasks_folder_in_the_clone_at_once()
    {
        var png = TasksFixture.TinyPng(500);

        var markdown = Add(png);

        Assert.Equal([CloneFile(markdown)], f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Equal(png, SavedBytes(markdown));
    }

    [Fact]
    public void Gives_each_image_its_own_id_and_keeps_both()
    {
        var first = Add(TasksFixture.TinyPng(300));
        var second = Add(TasksFixture.TinyPng(300));

        Assert.NotEqual(first, second);
        Assert.Equal(2, f.Files.AllFilesUnder(f.CloneImages(id)).Count);
    }

    [Theory]
    [InlineData("A [bracketed] name", @"A \[bracketed\] name")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("plain", "plain")]
    public void Escapes_brackets_and_backslashes_in_the_alt_text(string alt, string escaped)
    {
        var markdown = Add(TasksFixture.TinyPng(), alt);

        Assert.StartsWith($"![{escaped}](images/{id}/", markdown);
    }

    // The shrinking rules

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    public void Keeps_a_png_or_jpeg_of_at_most_1_MB_byte_for_byte(ImageFormat format)
    {
        var image = FakeImageEncoder.Image(format, 800, 600, 500_000);

        var markdown = Add(image);

        Assert.Equal(image, SavedBytes(markdown));
        Assert.Equal(format == ImageFormat.Png ? "png" : "jpg", Ext(markdown));
        Assert.Empty(f.Encoder.Calls);
    }

    [Fact]
    public void Scales_a_big_png_down_to_1920_pixels_on_its_longest_side_and_keeps_it_as_png()
    {
        f.Encoder.OutputSize = (_, _, _, _) => 600_000;
        var image = FakeImageEncoder.Image(ImageFormat.Png, 4000, 2000, TooBig);

        var markdown = Add(image);

        Assert.Equal(new EncoderCall(ImageFormat.Png, 1920, null), Assert.Single(f.Encoder.Calls));
        Assert.Equal("png", Ext(markdown));
        Assert.Equal(new ImageInfo(ImageFormat.Png, 1920, 960), f.Encoder.Read(SavedBytes(markdown)));
    }

    [Fact]
    public void Saves_a_png_as_jpeg_when_it_is_still_over_1_MB_at_1920_pixels()
    {
        f.Encoder.OutputSize = (format, _, _, _) => format == ImageFormat.Png ? TooBig : 400_000;
        var image = FakeImageEncoder.Image(ImageFormat.Png, 4000, 2000, TooBig);

        var markdown = Add(image);

        Assert.Equal("jpg", Ext(markdown));
        Assert.Equal(ImageFormat.Jpeg, f.Encoder.Read(SavedBytes(markdown)).Format);
    }

    [Fact]
    public void Lowers_a_jpegs_quality_from_85_in_steps_of_10_until_it_fits()
    {
        f.Encoder.OutputSize = (_, _, _, quality) => quality >= 75 ? TooBig : 900_000;
        var image = FakeImageEncoder.Image(ImageFormat.Jpeg, 4000, 3000, TooBig);

        var markdown = Add(image);

        Assert.Equal([85, 75, 65], f.Encoder.Calls.Select(c => c.Quality));
        Assert.Equal(900_000, SavedBytes(markdown).Length);
    }

    [Fact]
    public void Converts_other_image_formats_to_png()
    {
        var image = FakeImageEncoder.Image(ImageFormat.Other, 300, 200, 20_000);

        var markdown = Add(image);

        Assert.Equal("png", Ext(markdown));
        Assert.Equal(ImageFormat.Png, f.Encoder.Read(SavedBytes(markdown)).Format);
    }

    // What it never touches

    [Fact]
    public void Leaves_the_tasks_file_and_the_task_list_unchanged()
    {
        var fileBefore = f.Files.ReadFile(f.TaskFile(id));
        var taskBefore = f.Tasks.All.Single();

        Add(TasksFixture.TinyPng());

        Assert.Equal(fileBefore, f.Files.ReadFile(f.TaskFile(id)));
        TaskAssert.SameTask(taskBefore, f.Tasks.All.Single());
    }

    [Fact]
    public void Writes_nothing_on_this_device()
    {
        Add(TasksFixture.TinyPng());

        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Leaves_the_images_waiting_for_the_open_edit_in_place()
    {
        f.Tasks.SaveImage(id, TasksFixture.TinyPng(400), "edit pic");
        var waiting = f.Files.AllFilesUnder(f.Device).Order().ToList();
        var bytes = waiting.Select(f.Files.ReadFile).ToList();

        Add(TasksFixture.TinyPng(500));

        var after = f.Files.AllFilesUnder(f.Device).Order().ToList();
        Assert.Equal(waiting, after);
        Assert.Equal(bytes, after.Select(f.Files.ReadFile).ToList());
    }

    [Fact]
    public void Leaves_a_kept_draft_and_its_images_exactly_as_they_were()
    {
        var draft = f.Draft("Keep me", "text", "work");
        f.Tasks.SaveImage(draft.TaskId, TasksFixture.TinyPng(), "pic");
        f.Tasks.KeepDraft(draft);
        var before = f.Files.AllFilesUnder(f.Device).Order().ToList();
        var bytes = before.Select(f.Files.ReadFile).ToList();

        Add(TasksFixture.TinyPng(500));

        var after = f.Files.AllFilesUnder(f.Device).Order().ToList();
        Assert.Equal(before, after);
        Assert.Equal(bytes, after.Select(f.Files.ReadFile).ToList());
        Assert.Equal(draft, f.Tasks.LoadDraft(), DraftComparer.Instance);
    }

    // Notices and the log

    [Fact]
    public void Reports_one_local_save_and_raises_no_changed()
    {
        var (saved, changed) = CountNotices();

        Add(TasksFixture.TinyPng());

        Assert.Equal(1, saved());
        Assert.Equal(0, changed());
    }

    [Fact]
    public void Logs_one_information_entry_naming_the_task_id()
    {
        var before = f.Logs.Events.Count;

        Add(TasksFixture.TinyPng());

        f.Logs.After(before).SingleAt(LogEventLevel.Information, id);
    }

    // Lifetime: the image stays until its task is deleted

    [Fact]
    public void Keeps_the_image_when_the_open_edit_is_saved()
    {
        var markdown = Add(TasksFixture.TinyPng(500));
        var started = f.Tasks.All.Single();

        f.Tasks.SaveEdit(started, new TaskDraft(id, "Retitled", markdown, started.Tags));

        Assert.True(f.Files.FileExists(CloneFile(markdown)));
    }

    [Fact]
    public void Keeps_the_image_when_the_open_edits_images_are_discarded()
    {
        var markdown = Add(TasksFixture.TinyPng(500));
        f.Tasks.SaveImage(id, TasksFixture.TinyPng(400), "edit pic");

        f.Tasks.DiscardEditImages(id);

        Assert.True(f.Files.FileExists(CloneFile(markdown)));
    }

    [Fact]
    public void Keeps_the_image_when_the_draft_is_kept_discarded_or_added()
    {
        var markdown = Add(TasksFixture.TinyPng(500));
        var draft = f.Draft("A draft");
        f.Tasks.KeepDraft(draft);
        f.Tasks.DiscardDraft();
        f.Tasks.Add(f.Draft("Another"));

        Assert.True(f.Files.FileExists(CloneFile(markdown)));
    }

    [Fact]
    public void Keeps_the_image_when_nothing_links_to_it()
    {
        var markdown = Add(TasksFixture.TinyPng(500));

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.DoesNotContain(markdown, f.Tasks.All.Single().Description);
        Assert.True(f.Files.FileExists(CloneFile(markdown)));
    }

    [Fact]
    public void Deletes_the_image_with_its_task()
    {
        Add(TasksFixture.TinyPng(500));

        f.Tasks.Delete(id);

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
    }

    // Refusals, before any change

    private void AssertNothingChanged(Func<int> saved, Func<int> changed, int logged)
    {
        Assert.Empty(f.Files.AllFilesUnder(Path.Combine(f.Clone, "images")));
        Assert.Empty(f.Files.AllFilesUnder(f.Device));
        Assert.Equal(0, saved());
        Assert.Equal(0, changed());
        Assert.Empty(f.Logs.After(logged));
    }

    [Fact]
    public void Refuses_a_task_that_does_not_exist()
    {
        var (saved, changed) = CountNotices();
        var logged = f.Logs.Events.Count;

        Assert.Throws<TaskNotFoundException>(() => f.Tasks.AddImage(TasksFixture.Id(99), TasksFixture.TinyPng(), "pic"));

        AssertNothingChanged(saved, changed, logged);
    }

    [Fact]
    public void Refuses_a_task_whose_file_was_skipped_for_a_newer_version()
    {
        var future = TasksFixture.Id(8);
        f.Seed(future, "From the future", version: 99);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Throws<TaskNotFoundException>(() => f.Tasks.AddImage(future, TasksFixture.TinyPng(), "pic"));

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(future)));
    }

    [Fact]
    public void Refuses_a_task_that_was_deleted()
    {
        f.Tasks.Delete(id);

        Assert.Throws<TaskNotFoundException>(() => Add(TasksFixture.TinyPng()));

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
    }

    [Fact]
    public void Refuses_bytes_that_are_not_an_image()
    {
        var (saved, changed) = CountNotices();
        var logged = f.Logs.Events.Count;

        Assert.Throws<InvalidDataException>(() => Add([1, 2, 3, 4]));

        AssertNothingChanged(saved, changed, logged);
        Assert.Empty(f.Encoder.Calls);
    }

    [Fact]
    public void Refuses_an_image_that_cannot_be_shrunk_to_1_MB()
    {
        f.Encoder.OutputSize = (_, _, _, _) => TooBig;
        var (saved, changed) = CountNotices();
        var logged = f.Logs.Events.Count;

        Assert.Throws<ImageTooLargeException>(() => Add(FakeImageEncoder.Image(ImageFormat.Png, 4000, 3000, TooBig)));

        AssertNothingChanged(saved, changed, logged);
    }

    [Fact]
    public void Refuses_when_no_folder_is_set()
    {
        f.Tasks.UseRepositoryFolder(null);
        var (saved, changed) = CountNotices();

        Assert.Throws<NoRepositoryFolderException>(() => Add(TasksFixture.TinyPng()));

        Assert.Equal(0, saved());
        Assert.Equal(0, changed());
        Assert.Empty(f.Files.AllFilesUnder(f.Device));
        Assert.Empty(f.Files.AllFilesUnder(Path.Combine(f.Clone, "images")));
    }

    // Write failure

    [Fact]
    public void Throws_the_systems_exception_and_reports_no_local_save_when_the_image_cannot_be_written()
    {
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "images"));
        var (saved, changed) = CountNotices();

        Assert.Throws<IOException>(() => Add(TasksFixture.TinyPng()));

        Assert.Equal(0, saved());
        Assert.Equal(0, changed());
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
    }
}
