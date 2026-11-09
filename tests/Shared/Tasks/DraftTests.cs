using System.Text.Json;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>The one draft of a new task this device keeps outside the clone.</summary>
[Unit]
public sealed class DraftTests
{
    private readonly TasksFixture f = new();

    private static byte[] SomePng() => FakeImageEncoder.Image(ImageFormat.Png, 20, 10, 300);

    [Fact]
    public void Starts_an_empty_draft()
    {
        var draft = f.Tasks.StartDraft();

        Assert.Equal("", draft.Title);
        Assert.Equal("", draft.Description);
        Assert.Empty(draft.Tags);
    }

    [Fact]
    public void Gives_a_new_draft_a_lowercase_hyphenated_uuid_version_7_id()
    {
        var draft = f.Tasks.StartDraft();

        Assert.Equal(36, draft.TaskId.Length);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", draft.TaskId);
    }

    [Fact]
    public void Gives_each_new_draft_its_own_id()
    {
        Assert.NotEqual(f.Tasks.StartDraft().TaskId, f.Tasks.StartDraft().TaskId);
    }

    [Fact]
    public void Writes_nothing_when_a_draft_starts()
    {
        f.Tasks.StartDraft();

        Assert.Empty(f.Files.ListFiles(f.Device));
        Assert.False(f.Files.FileExists(f.DraftFile));
        Assert.Null(f.Tasks.LoadDraft());
    }

    [Fact]
    public void Loads_no_draft_when_none_is_kept()
    {
        Assert.Null(f.Tasks.LoadDraft());
    }

    [Fact]
    public void Writes_the_kept_draft_to_draft_json_in_the_device_folder()
    {
        var draft = f.Draft("Title", "Body", "a", "b");

        f.Tasks.KeepDraft(draft);

        using var json = f.ReadJson(f.DraftFile);
        Assert.Equal(["description", "id", "tags", "title", "version"],
            json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(draft.TaskId, json.RootElement.GetProperty("id").GetString());
        Assert.Equal("Title", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("Body", json.RootElement.GetProperty("description").GetString());
        Assert.Equal(["a", "b"], json.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("version").ValueKind);
    }

    [Fact]
    public void Loads_the_kept_draft_as_it_was_kept()
    {
        var draft = f.Draft("Title", "Line one\nLine two", "a", "b");
        f.Tasks.KeepDraft(draft);

        var loaded = f.Tasks.LoadDraft();

        Assert.NotNull(loaded);
        Assert.Equal(draft.TaskId, loaded.TaskId);
        Assert.Equal("Title", loaded.Title);
        Assert.Equal("Line one\nLine two", loaded.Description);
        Assert.Equal(["a", "b"], loaded.Tags);
    }

    [Fact]
    public void Loads_the_kept_draft_after_the_app_restarts()
    {
        var draft = f.Draft("Title", "Body", "a");
        f.Tasks.KeepDraft(draft);

        var loaded = f.Restarted().LoadDraft();

        Assert.Equal(draft, loaded, DraftComparer.Instance);
    }

    [Fact]
    public void Keeps_a_draft_with_only_a_title()
    {
        f.Tasks.KeepDraft(f.Draft("Just a title"));

        Assert.Equal("Just a title", f.Tasks.LoadDraft()!.Title);
    }

    [Fact]
    public void Keeps_a_draft_with_only_tags()
    {
        f.Tasks.KeepDraft(f.Draft("", "", "tag"));

        Assert.Equal(["tag"], f.Tasks.LoadDraft()!.Tags);
    }

    [Fact]
    public void Keeps_a_draft_when_no_repository_folder_is_set()
    {
        var draft = f.Draft("Offline", "text");

        f.Tasks.KeepDraft(draft);

        Assert.Equal("Offline", f.Tasks.LoadDraft()!.Title);
    }

    [Fact]
    public void Replaces_the_kept_draft_with_the_new_one()
    {
        f.Tasks.KeepDraft(f.Draft("Old"));
        var newer = f.Draft("New");

        f.Tasks.KeepDraft(newer);

        var loaded = f.Tasks.LoadDraft()!;
        Assert.Equal("New", loaded.Title);
        Assert.Equal(newer.TaskId, loaded.TaskId);
    }

    [Fact]
    public void Deletes_the_images_kept_for_another_task_id_when_the_draft_is_replaced()
    {
        var old = f.Draft("Old");
        f.Tasks.SaveDraftImage(old.TaskId, SomePng(), "pic");
        f.Tasks.KeepDraft(old);

        f.Tasks.KeepDraft(f.Draft("New"));

        Assert.Empty(f.Files.ListFiles(f.DraftImages(old.TaskId)));
    }

    [Fact]
    public void Keeps_the_images_of_the_draft_it_keeps()
    {
        var draft = f.Draft("Mine");
        f.Tasks.SaveDraftImage(draft.TaskId, SomePng(), "pic");
        f.Tasks.KeepDraft(draft);

        f.Tasks.KeepDraft(draft with { Title = "Mine, edited" });

        Assert.Single(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
    }

    [Theory]
    [InlineData("", "", new string[0])]
    [InlineData("   ", "", new string[0])]
    [InlineData("", " \n  \n", new string[0])]
    [InlineData(" ", "\t ", new string[0])]
    public void Deletes_the_kept_draft_and_its_images_when_the_new_draft_is_blank(string title, string description, string[] tags)
    {
        var kept = f.Draft("Something");
        f.Tasks.SaveDraftImage(kept.TaskId, SomePng(), "pic");
        f.Tasks.KeepDraft(kept);

        f.Tasks.KeepDraft(kept with { Title = title, Description = description, Tags = tags });

        Assert.Null(f.Tasks.LoadDraft());
        Assert.False(f.Files.FileExists(f.DraftFile));
        Assert.Empty(f.Files.ListFiles(f.DraftImages(kept.TaskId)));
    }

    [Fact]
    public void Writes_nothing_when_a_blank_draft_is_kept_and_none_was_kept()
    {
        f.Tasks.KeepDraft(f.Draft("", ""));

        Assert.False(f.Files.FileExists(f.DraftFile));
        Assert.Null(f.Tasks.LoadDraft());
    }

    [Fact]
    public void Throws_the_systems_exception_when_the_draft_cannot_be_written()
    {
        f.Files.FailWritesUnder(f.Device);

        Assert.Throws<IOException>(() => f.Tasks.KeepDraft(f.Draft("Title")));
    }

    [Fact]
    public void Refuses_to_load_a_draft_that_cannot_be_read()
    {
        f.WriteText(f.DraftFile, "{ this is not json");

        Assert.Throws<InvalidDataException>(() => f.Tasks.LoadDraft());
    }

    [Fact]
    public void Refuses_to_load_a_kept_draft_that_cannot_be_read_from_the_disk_keeping_the_cause()
    {
        f.Tasks.KeepDraft(f.Draft("Unreachable"));
        f.Files.FailReadsUnder(f.Device);

        var failure = Assert.Throws<InvalidDataException>(() => f.Tasks.LoadDraft());

        Assert.IsType<IOException>(failure.InnerException);
    }

    [Fact]
    public void Keeps_the_draft_file_when_it_cannot_be_read_from_the_disk()
    {
        var draft = f.Draft("Unreachable");
        f.Tasks.KeepDraft(draft);
        f.Files.FailReadsUnder(f.Device);

        Assert.Throws<InvalidDataException>(() => f.Tasks.LoadDraft());

        f.Files.StopFailing();
        Assert.Equal("Unreachable", f.Tasks.LoadDraft()!.Title);
    }

    [Fact]
    public void Loads_no_draft_without_failing_when_the_device_has_none_even_if_reads_fail()
    {
        f.Files.FailReadsUnder(f.Device);

        Assert.Null(f.Tasks.LoadDraft());
    }

    [Fact]
    public void Refuses_to_load_a_draft_that_lacks_its_fields()
    {
        f.WriteText(f.DraftFile, "{}");

        Assert.Throws<InvalidDataException>(() => f.Tasks.LoadDraft());
    }

    [Fact]
    public void Leaves_a_draft_that_cannot_be_read_unchanged()
    {
        f.WriteText(f.DraftFile, "{ this is not json");

        Assert.Throws<InvalidDataException>(() => f.Tasks.LoadDraft());

        Assert.Equal("{ this is not json", f.ReadText(f.DraftFile));
    }

    [Fact]
    public void Replaces_a_draft_that_cannot_be_read_when_a_new_one_is_kept()
    {
        f.WriteText(f.DraftFile, "{ this is not json");

        f.Tasks.KeepDraft(f.Draft("Fresh"));

        Assert.Equal("Fresh", f.Tasks.LoadDraft()!.Title);
    }

    [Fact]
    public void Deletes_a_draft_that_cannot_be_read_when_it_is_discarded()
    {
        f.WriteText(f.DraftFile, "{ this is not json");

        f.Tasks.DiscardDraft();

        Assert.False(f.Files.FileExists(f.DraftFile));
        Assert.Null(f.Tasks.LoadDraft());
    }

    [Fact]
    public void Deletes_a_kept_draft_whose_id_is_already_a_task_and_loads_none()
    {
        var draft = f.Draft("Saved just before the app stopped");
        f.Tasks.SaveDraftImage(draft.TaskId, SomePng(), "pic");
        f.Tasks.KeepDraft(draft);
        f.Seed(draft.TaskId, title: "Saved just before the app stopped");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Null(f.Tasks.LoadDraft());

        Assert.False(f.Files.FileExists(f.DraftFile));
        Assert.Empty(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
        Assert.Single(f.Tasks.All);
    }

    [Fact]
    public void Loads_a_kept_draft_whose_id_is_not_a_task()
    {
        f.Seed(TasksFixture.Id(1));
        f.Tasks.KeepDraft(f.Draft("Still a draft"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal("Still a draft", f.Tasks.LoadDraft()!.Title);
    }

    [Fact]
    public void Deletes_the_kept_draft_and_its_images_when_it_is_discarded()
    {
        var draft = f.Draft("Drop me");
        f.Tasks.SaveDraftImage(draft.TaskId, SomePng(), "pic");
        f.Tasks.KeepDraft(draft);

        f.Tasks.DiscardDraft();

        Assert.Null(f.Tasks.LoadDraft());
        Assert.False(f.Files.FileExists(f.DraftFile));
        Assert.Empty(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
    }

    [Fact]
    public void Deletes_images_saved_before_the_draft_was_first_kept_when_it_is_discarded()
    {
        var draft = f.Draft("Never kept");
        f.Tasks.SaveDraftImage(draft.TaskId, SomePng(), "pic");

        f.Tasks.DiscardDraft();

        Assert.Empty(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
    }

    [Fact]
    public void Deletes_images_saved_for_a_draft_that_is_not_the_kept_one_when_it_is_discarded()
    {
        var kept = f.Draft("Kept");
        var unkept = f.Draft("Never kept");
        f.Tasks.KeepDraft(kept);
        f.Tasks.SaveDraftImage(unkept.TaskId, SomePng(), "pic");

        f.Tasks.DiscardDraft();

        Assert.Empty(f.Files.ListFiles(f.DraftImages(unkept.TaskId)));
    }

    [Fact]
    public void Discards_nothing_without_failing_when_no_draft_is_kept()
    {
        f.Tasks.DiscardDraft();

        Assert.Null(f.Tasks.LoadDraft());
    }

    [Fact]
    public void Writes_nothing_inside_the_clone_for_a_draft_before_it_is_added()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Not yet", "text");

        f.Tasks.KeepDraft(draft);
        f.Tasks.SaveDraftImage(draft.TaskId, SomePng(), "pic");
        f.Tasks.KeepDraft(draft with { Title = "Still not" });

        Assert.Empty(f.Files.ListFiles(f.Clone));
        Assert.Empty(f.Files.ListFiles(Path.Combine(f.Clone, "tasks")));
        Assert.Empty(f.Files.ListFiles(f.CloneImages(draft.TaskId)));
        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Keeps_the_same_task_id_across_keeping_and_loading()
    {
        var draft = f.Tasks.StartDraft() with { Title = "Same id" };

        f.Tasks.KeepDraft(draft);

        Assert.Equal(draft.TaskId, f.Tasks.LoadDraft()!.TaskId);
        Assert.Equal(draft.TaskId, f.Tasks.LoadDraft()!.TaskId);
    }

    [Fact]
    public void Adds_the_task_with_the_id_the_draft_started_with()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Tasks.StartDraft() with { Title = "From a started draft" };

        var task = f.Tasks.Add(draft);

        Assert.Equal(draft.TaskId, task.Id);
    }

    [Fact]
    public void Loads_no_draft_once_its_task_is_added()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Saved");
        f.Tasks.KeepDraft(draft);
        f.Tasks.Add(draft);

        Assert.Null(f.Restarted().LoadDraft());
    }

    /// <summary>Compares drafts by their text, since a draft holds a list.</summary>
    private sealed class DraftComparer : IEqualityComparer<TaskDraft?>
    {
        public static readonly DraftComparer Instance = new();

        public bool Equals(TaskDraft? x, TaskDraft? y) =>
            x is not null && y is not null && x.TaskId == y.TaskId && x.Title == y.Title
            && x.Description == y.Description && x.Tags.SequenceEqual(y.Tags);

        public int GetHashCode(TaskDraft? obj) => obj?.TaskId.GetHashCode() ?? 0;
    }
}
