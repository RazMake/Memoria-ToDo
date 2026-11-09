using System.Text.Json;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Adding a task: what it checks, what it returns, and what it does to the list.</summary>
[Unit]
public sealed class AddTaskTests
{
    private readonly TasksFixture f = new();

    [Fact]
    public void Refuses_to_add_when_no_folder_is_set()
    {
        Assert.Throws<NoRepositoryFolderException>(() => f.Tasks.Add(f.Draft()));
    }

    [Fact]
    public void Refuses_to_add_after_the_folder_is_cleared()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Tasks.UseRepositoryFolder(null);

        Assert.Throws<NoRepositoryFolderException>(() => f.Tasks.Add(f.Draft()));
    }

    [Fact]
    public void Writes_nothing_when_no_folder_is_set()
    {
        var draft = f.Draft();

        Assert.Throws<NoRepositoryFolderException>(() => f.Tasks.Add(draft));

        f.Tasks.UseRepositoryFolder(f.Clone);
        Assert.Empty(f.Tasks.All);
        Assert.Empty(f.Files.ListFiles(Path.Combine(f.Clone, "tasks")));
    }

    [Fact]
    public void Keeps_the_kept_draft_when_no_folder_is_set()
    {
        var draft = f.Draft("Keep me");
        f.Tasks.KeepDraft(draft);

        Assert.Throws<NoRepositoryFolderException>(() => f.Tasks.Add(draft));

        Assert.Equal("Keep me", f.Tasks.LoadDraft()!.Title);
    }

    [Fact]
    public void Trims_the_title()
    {
        var task = f.AddTask("  Buy milk  ");

        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("Buy milk", Assert.Single(f.Tasks.All).Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    [InlineData("two\r\nlines")]
    public void Refuses_a_title_that_is_empty_or_has_a_line_break(string title)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(f.Draft(title)));
    }

    [Fact]
    public void Writes_nothing_and_keeps_the_draft_when_the_title_is_refused()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("  ", "text");
        f.Tasks.KeepDraft(draft);

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(draft));

        Assert.Empty(f.Files.ListFiles(Path.Combine(f.Clone, "tasks")));
        Assert.Empty(f.Tasks.All);
        Assert.NotNull(f.Tasks.LoadDraft());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public void Refuses_a_tag_that_is_empty_or_has_a_line_break(string tag)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(f.Draft("Title", "", "fine", tag)));

        Assert.Empty(f.Files.ListFiles(Path.Combine(f.Clone, "tasks")));
    }

    [Fact]
    public void Writes_the_task_to_a_file_named_by_its_id()
    {
        var draft = f.Draft();
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.Add(draft);

        Assert.True(f.Files.FileExists(f.TaskFile(draft.TaskId)));
    }

    [Fact]
    public void Gives_the_task_the_drafts_id()
    {
        var draft = f.Draft();
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = f.Tasks.Add(draft);

        Assert.Equal(draft.TaskId, task.Id);
    }

    [Fact]
    public void Returns_the_task_that_is_first_in_the_list()
    {
        f.Seed(TasksFixture.Id(1), title: "Older", rank: "a0");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = f.Tasks.Add(f.Draft("Newest"));

        var first = f.Tasks.All[0];
        Assert.Equal(task.Id, first.Id);
        Assert.Equal("Newest", first.Title);
        Assert.Equal(task.Rank, first.Rank);
        Assert.Equal(2, f.Tasks.All.Count);
    }

    [Fact]
    public void Raises_changed_once_when_a_task_is_added()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var changes = f.CountChanges();

        f.Tasks.Add(f.Draft());

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Starts_a_new_task_open_with_equal_created_and_updated_times()
    {
        var task = f.AddTask();

        Assert.Equal(TasksFixture.Start, task.Created);
        Assert.Equal(task.Created, task.Updated);
        Assert.Null(task.Resolved);
    }

    [Fact]
    public void Takes_the_time_from_the_time_provider_when_it_adds()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Time.Advance(TimeSpan.FromHours(5));

        var task = f.Tasks.Add(f.Draft());

        Assert.Equal(TasksFixture.Start.AddHours(5), task.Created);
        Assert.Equal(TasksFixture.Start.AddHours(5), task.Updated);
    }

    [Fact]
    public void Keeps_the_description_with_each_line_break_as_a_line_feed()
    {
        var task = f.AddTask("T", "one\r\ntwo\nthree");

        Assert.Equal("one\ntwo\nthree", task.Description);
        using var json = f.ReadJson(f.TaskFile(task.Id));
        Assert.Equal("one\ntwo\nthree", json.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public void Keeps_the_tags_in_the_order_they_were_added()
    {
        var task = f.AddTask("T", "", "zeta", "alpha", "mid");

        Assert.Equal(["zeta", "alpha", "mid"], task.Tags);
    }

    [Fact]
    public void Trims_the_tags()
    {
        var task = f.AddTask("T", "", "  work ");

        Assert.Equal(["work"], task.Tags);
    }

    [Fact]
    public void Drops_a_tag_the_task_already_has_ignoring_case()
    {
        var task = f.AddTask("T", "", "work", "WORK", "home", "Work");

        Assert.Equal(["work", "home"], task.Tags);
    }

    [Fact]
    public void Spells_a_tag_as_the_matching_tag_in_use()
    {
        f.Seed(TasksFixture.Id(1), tags: ["Work"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = f.Tasks.Add(f.Draft("T", "", "work", "home"));

        Assert.Equal(["Work", "home"], task.Tags);
    }

    [Fact]
    public void Saves_the_tags_the_task_ends_up_with()
    {
        f.Seed(TasksFixture.Id(1), tags: ["Work"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = f.Tasks.Add(f.Draft("T", "", "work", "WORK"));

        using var json = f.ReadJson(f.TaskFile(task.Id));
        var tags = json.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
        Assert.Equal(["Work"], tags);
    }

    [Fact]
    public void Keeps_the_task_after_the_app_restarts()
    {
        var task = f.AddTask("Persistent", "body", "tag");

        var again = f.Restarted();
        again.UseRepositoryFolder(f.Clone);

        var read = Assert.Single(again.All);
        Assert.Equal(task.Id, read.Id);
        Assert.Equal("Persistent", read.Title);
        Assert.Equal("body", read.Description);
        Assert.Equal(task.Rank, read.Rank);
        Assert.Equal(["tag"], read.Tags);
        Assert.Equal(task.Created, read.Created);
    }

    [Fact]
    public void Throws_the_systems_exception_when_the_file_cannot_be_written()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.Throws<IOException>(() => f.Tasks.Add(f.Draft()));
    }

    [Fact]
    public void Adds_nothing_to_the_list_when_the_file_cannot_be_written()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        var changes = f.CountChanges();

        Assert.Throws<IOException>(() => f.Tasks.Add(f.Draft()));

        Assert.Empty(f.Tasks.All);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Keeps_the_draft_and_its_images_when_the_file_cannot_be_written()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Keep", "text");
        f.Tasks.KeepDraft(draft);
        f.Tasks.SaveDraftImage(draft.TaskId, Fakes.FakeImageEncoder.Image(ImageFormat.Png, 10, 10), "pic");
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.Throws<IOException>(() => f.Tasks.Add(draft));

        Assert.Equal("Keep", f.Tasks.LoadDraft()!.Title);
        Assert.Single(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
    }

    [Fact]
    public void Keeps_the_draft_when_its_images_cannot_be_moved_into_the_clone()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Keep", "text");
        f.Tasks.KeepDraft(draft);
        f.Tasks.SaveDraftImage(draft.TaskId, Fakes.FakeImageEncoder.Image(ImageFormat.Png, 10, 10), "pic");
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "images"));

        Assert.Throws<IOException>(() => f.Tasks.Add(draft));

        Assert.True(f.Files.FileExists(f.DraftFile));
        Assert.Equal("Keep", f.Tasks.LoadDraft()!.Title);
    }

    [Fact]
    public void Moves_the_drafts_images_from_the_device_into_the_clone()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("With pictures");
        var png = Fakes.FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 500);
        f.Tasks.SaveDraftImage(draft.TaskId, png, "pic");
        var name = Path.GetFileName(Assert.Single(f.Files.ListFiles(f.DraftImages(draft.TaskId))));

        f.Tasks.Add(draft);

        Assert.Empty(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
        Assert.Equal(png, f.Files.ReadFile(Path.Combine(f.CloneImages(draft.TaskId), name)));
    }

    [Fact]
    public void Deletes_the_kept_draft_when_the_task_is_added()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Done");
        f.Tasks.KeepDraft(draft);

        f.Tasks.Add(draft);

        Assert.Null(f.Tasks.LoadDraft());
        Assert.False(f.Files.FileExists(f.DraftFile));
    }

    /// <summary>A kept draft with two images, named in the order the device folder lists them, and the clone opened.</summary>
    private (TaskDraft Draft, string[] ImageNames, byte[][] Images) KeepDraftWithTwoImages()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Keep", "text");
        f.Tasks.KeepDraft(draft);
        byte[][] images =
        [
            Fakes.FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 500),
            Fakes.FakeImageEncoder.Image(ImageFormat.Png, 20, 10, 600),
        ];
        foreach (var image in images)
            f.Tasks.SaveDraftImage(draft.TaskId, image, "pic");
        var names = f.Files.ListFiles(f.DraftImages(draft.TaskId)).Select(Path.GetFileName).Cast<string>().ToArray();
        Assert.Equal(2, names.Length);
        return (draft, names, images);
    }

    private void AssertDraftIsWhole(TaskDraft draft, string[] imageNames, byte[][] images)
    {
        Assert.Equal("Keep", Assert.IsType<TaskDraft>(f.Tasks.LoadDraft()).Title);
        Assert.Equal(imageNames.Order(), f.Files.ListFiles(f.DraftImages(draft.TaskId)).Select(Path.GetFileName).Order());
        foreach (var name in imageNames)
            Assert.Contains(f.Files.ReadFile(Path.Combine(f.DraftImages(draft.TaskId), name)), images);
        Assert.False(f.Files.FileExists(f.TaskFile(draft.TaskId)));
        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Keeps_the_draft_whole_when_the_second_image_cannot_be_moved_after_the_first_was()
    {
        var (draft, names, images) = KeepDraftWithTwoImages();
        var blocker = Path.Combine(f.CloneImages(draft.TaskId), names[1]);
        f.Files.WriteFile(blocker, [9, 9, 9]);

        Assert.Throws<IOException>(() => f.Tasks.Add(draft));

        AssertDraftIsWhole(draft, names, images);
        Assert.Equal([blocker], f.Files.ListFiles(f.CloneImages(draft.TaskId)));
        Assert.Equal([9, 9, 9], f.Files.ReadFile(blocker));
    }

    [Fact]
    public void Adds_the_task_with_both_images_when_retried_after_a_failed_image_move()
    {
        var (draft, names, images) = KeepDraftWithTwoImages();
        var blocker = Path.Combine(f.CloneImages(draft.TaskId), names[1]);
        f.Files.WriteFile(blocker, [9, 9, 9]);
        Assert.Throws<IOException>(() => f.Tasks.Add(draft));
        f.Files.DeleteFile(blocker);

        var task = f.Tasks.Add(draft);

        Assert.Equal(draft.TaskId, task.Id);
        Assert.Equal(names.Order(), f.Files.ListFiles(f.CloneImages(draft.TaskId)).Select(Path.GetFileName).Order());
        Assert.Empty(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
        Assert.Null(f.Tasks.LoadDraft());
        Assert.Contains(f.Files.ReadFile(Path.Combine(f.CloneImages(draft.TaskId), names[0])), images);
    }

    [Fact]
    public void Keeps_the_draft_whole_when_the_draft_file_cannot_be_deleted_after_the_images_moved()
    {
        var (draft, names, images) = KeepDraftWithTwoImages();
        f.Files.FailDeletesUnder(f.DraftFile);
        var changes = f.CountChanges();

        Assert.Throws<IOException>(() => f.Tasks.Add(draft));

        AssertDraftIsWhole(draft, names, images);
        Assert.Empty(f.Files.ListFiles(f.CloneImages(draft.TaskId)));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Keeps_the_draft_whole_when_the_images_folder_cannot_be_deleted_after_the_images_moved()
    {
        var (draft, names, images) = KeepDraftWithTwoImages();
        f.Files.FailDeletesUnder(f.DraftImages(draft.TaskId));

        Assert.Throws<IOException>(() => f.Tasks.Add(draft));

        AssertDraftIsWhole(draft, names, images);
        Assert.Empty(f.Files.ListFiles(f.CloneImages(draft.TaskId)));
    }

    [Fact]
    public void Adds_the_task_when_retried_after_a_failed_draft_cleanup()
    {
        var (draft, names, _) = KeepDraftWithTwoImages();
        f.Files.FailDeletesUnder(f.DraftFile);
        Assert.Throws<IOException>(() => f.Tasks.Add(draft));
        f.Files.StopFailing();

        f.Tasks.Add(draft);

        Assert.Equal(names.Order(), f.Files.ListFiles(f.CloneImages(draft.TaskId)).Select(Path.GetFileName).Order());
        Assert.Null(f.Tasks.LoadDraft());
        Assert.Single(f.Tasks.All);
    }

    [Fact]
    public void Refuses_an_id_whose_task_file_was_skipped_for_a_newer_version_and_leaves_that_file_unchanged()
    {
        f.Seed(TasksFixture.Id(7), title: "From the future", version: 99);
        var before = f.ReadText(f.TaskFile(TasksFixture.Id(7)));
        f.Tasks.UseRepositoryFolder(f.Clone);
        Assert.Empty(f.Tasks.All);

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(f.Draft("Same id") with { TaskId = TasksFixture.Id(7) }));

        Assert.Equal(before, f.ReadText(f.TaskFile(TasksFixture.Id(7))));
        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Refuses_an_id_whose_task_file_was_skipped_as_unreadable_and_leaves_that_file_unchanged()
    {
        f.WriteText(f.TaskFile(TasksFixture.Id(7)), "{ this is not json");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(f.Draft("Same id") with { TaskId = TasksFixture.Id(7) }));

        Assert.Equal("{ this is not json", f.ReadText(f.TaskFile(TasksFixture.Id(7))));
    }

    [Fact]
    public void Keeps_the_draft_when_its_id_names_a_skipped_task_file()
    {
        f.Seed(TasksFixture.Id(7), version: 99);
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Keep") with { TaskId = TasksFixture.Id(7) };
        f.Tasks.KeepDraft(draft);

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(draft));

        Assert.Equal("Keep", f.Tasks.LoadDraft()!.Title);
    }

    [Theory]
    [InlineData("a0!")]
    [InlineData("a0 ")]
    [InlineData("a1-")]
    [InlineData("a0\u00e9")]
    [InlineData("b00!5")]
    public void Refuses_to_add_when_the_top_rank_has_an_invalid_fraction_character(string rank)
    {
        f.Seed(TasksFixture.Id(1), rank: rank);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Throws<InvalidDataException>(() => f.Tasks.Add(f.Draft()));

        Assert.Single(f.Files.ListFiles(Path.Combine(f.Clone, "tasks")));
    }

    [Fact]
    public void Still_adds_before_a_top_rank_with_a_valid_fraction()
    {
        f.Seed(TasksFixture.Id(1), rank: "a0V");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = f.Tasks.Add(f.Draft());

        Assert.True(string.CompareOrdinal(task.Rank, "a0V") < 0);
    }

    [Fact]
    public void Writes_each_new_task_to_its_own_file()
    {
        var first = f.AddTask("First");
        var second = f.Tasks.Add(f.Draft("Second"));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, f.Files.ListFiles(Path.Combine(f.Clone, "tasks")).Count);
        using var json = f.ReadJson(f.TaskFile(first.Id));
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
    }
}
