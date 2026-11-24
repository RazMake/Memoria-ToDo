using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// Adding a task from a title, a description, and a tag, as an assistant does: the same checks, rank, file, log entry, notices,
/// and local save report as adding a draft, with a new task ID, and never a touch on the kept draft or any images.
/// </summary>
[Unit]
public sealed class AddFromPartsTests
{
    private readonly TasksFixture f = new();

    private TaskItem Add(string title = "A task", string description = "", string? tag = null)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        return f.Tasks.Add(title, description, tag);
    }

    private IReadOnlyList<string> TaskFiles() => f.Files.ListFiles(Path.Combine(f.Clone, "tasks"));

    // What it adds

    [Fact]
    public void Adds_an_open_task_with_the_title_description_and_tag_and_returns_it_as_it_is_in_the_list()
    {
        var task = Add("Buy milk", "two litres", "home");

        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("two litres", task.Description);
        Assert.Equal(["home"], task.Tags);
        Assert.Null(task.Resolved);
        Assert.Equal(TasksFixture.Start, task.Created);
        Assert.Equal(task.Created, task.Updated);
        TaskAssert.SameTask(task, Assert.Single(f.Tasks.All));
    }

    [Fact]
    public void Adds_no_tag_when_the_tag_is_null()
    {
        Assert.Empty(Add("T", "", null).Tags);
    }

    [Fact]
    public void Trims_the_title_and_the_tag()
    {
        var task = Add("  Buy milk  ", "", "  work ");

        Assert.Equal("Buy milk", task.Title);
        Assert.Equal(["work"], task.Tags);
    }

    [Fact]
    public void Keeps_the_description_with_each_line_break_as_a_line_feed()
    {
        var task = Add("T", "one\r\ntwo\nthree");

        Assert.Equal("one\ntwo\nthree", task.Description);
    }

    [Fact]
    public void Spells_a_tag_as_the_matching_tag_in_use()
    {
        f.Seed(TasksFixture.Id(1), tags: ["Work"]);

        var task = Add("T", "", "work");

        Assert.Equal(["Work"], task.Tags);
    }

    [Fact]
    public void Gives_the_task_a_new_valid_id_and_writes_it_to_a_file_named_by_that_id()
    {
        var task = Add();

        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", task.Id);
        Assert.Equal([f.TaskFile(task.Id)], TaskFiles());
    }

    [Fact]
    public void Gives_each_added_task_its_own_id()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var first = f.Tasks.Add("One", "", null);
        var second = f.Tasks.Add("Two", "", null);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, f.Tasks.All.Count);
    }

    [Fact]
    public void Puts_the_new_task_at_the_top()
    {
        f.Seed(TasksFixture.Id(1), "Older", rank: "a0");

        var task = Add("Newest");

        Assert.Equal(task.Id, f.Tasks.All[0].Id);
        Assert.Equal(["Newest", "Older"], f.Titles);
    }

    [Fact]
    public void Keeps_the_task_after_the_app_restarts()
    {
        var task = Add("Persistent", "body", "tag");

        var again = f.Restarted();
        again.UseRepositoryFolder(f.Clone);

        var read = Assert.Single(again.All);
        TaskAssert.SameTask(task, read);
    }

    // Notices and the log

    [Fact]
    public void Raises_changed_once()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var changes = f.CountChanges();

        f.Tasks.Add("T", "", null);

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Reports_one_local_save()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var saved = 0;
        f.Repository.LocalSaved += (_, _) => saved++;

        f.Tasks.Add("T", "", null);

        Assert.Equal(1, saved);
    }

    [Fact]
    public void Logs_one_information_entry_naming_the_task_id_and_title()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Logs.Events.Count;

        var task = f.Tasks.Add("Buy milk", "", null);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, task.Id, "Buy milk");
    }

    // The draft and images stay untouched

    [Fact]
    public void Leaves_a_kept_draft_and_its_images_exactly_as_they_were()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Keep me", "text", "work");
        f.Tasks.SaveImage(draft.TaskId, TasksFixture.TinyPng(), "pic");
        f.Tasks.KeepDraft(draft);
        var deviceBefore = f.Files.AllFilesUnder(f.Device).Order().ToList();
        var bytesBefore = deviceBefore.Select(f.Files.ReadFile).ToList();

        f.Tasks.Add("Assistant task", "", "work");

        var deviceAfter = f.Files.AllFilesUnder(f.Device).Order().ToList();
        Assert.Equal(deviceBefore, deviceAfter);
        Assert.Equal(bytesBefore, deviceAfter.Select(f.Files.ReadFile).ToList());
        Assert.Equal(draft, f.Tasks.LoadDraft(), DraftComparer.Instance);
        Assert.False(f.Files.FileExists(f.TaskFile(draft.TaskId)));
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(draft.TaskId)));
    }

    [Fact]
    public void Leaves_the_images_waiting_for_an_open_edit_in_place()
    {
        f.SeedAndUse(TasksFixture.Id(7), "Existing");
        f.Tasks.SaveImage(TasksFixture.Id(7), TasksFixture.TinyPng(), "pic");
        var deviceBefore = f.Files.AllFilesUnder(f.Device).Order().ToList();

        f.Tasks.Add("Assistant task", "", null);

        Assert.Equal(deviceBefore, f.Files.AllFilesUnder(f.Device).Order());
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(TasksFixture.Id(7))));
    }

    [Fact]
    public void Writes_nothing_but_the_task_file_into_the_clone()
    {
        var task = Add();

        Assert.Equal([f.TaskFile(task.Id)], f.Files.AllFilesUnder(f.Clone));
    }

    // Refusals

    [Fact]
    public void Refuses_to_add_when_no_folder_is_set_and_leaves_a_kept_draft_alone()
    {
        var draft = f.Draft("Keep me");
        f.Tasks.KeepDraft(draft);

        Assert.Throws<NoRepositoryFolderException>(() => f.Tasks.Add("T", "", null));

        Assert.Equal("Keep me", f.Tasks.LoadDraft()!.Title);
        f.Tasks.UseRepositoryFolder(f.Clone);
        Assert.Empty(f.Tasks.All);
        Assert.Empty(TaskFiles());
    }

    [Fact]
    public void Refuses_to_add_after_the_folder_is_cleared()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Tasks.UseRepositoryFolder(null);

        Assert.Throws<NoRepositoryFolderException>(() => f.Tasks.Add("T", "", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    [InlineData("two\r\nlines")]
    public void Refuses_a_title_that_is_empty_or_has_a_line_break_before_any_change(string title)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var changes = f.CountChanges();
        var before = f.Logs.Events.Count;

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(title, "", null));

        Assert.Empty(TaskFiles());
        Assert.Empty(f.Tasks.All);
        Assert.Equal(0, changes.Count);
        Assert.Empty(f.Logs.After(before));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public void Refuses_a_tag_that_is_empty_or_has_a_line_break_before_any_change(string tag)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var changes = f.CountChanges();

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add("Title", "", tag));

        Assert.Empty(TaskFiles());
        Assert.Empty(f.Tasks.All);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Reports_no_local_save_when_a_refused_add_changed_nothing()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var saved = 0;
        f.Repository.LocalSaved += (_, _) => saved++;

        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add("", "", null));

        Assert.Equal(0, saved);
    }

    // File failure

    [Fact]
    public void Throws_the_systems_exception_and_changes_nothing_when_the_file_cannot_be_written()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        var changes = f.CountChanges();
        var saved = 0;
        f.Repository.LocalSaved += (_, _) => saved++;

        Assert.Throws<IOException>(() => f.Tasks.Add("T", "", null));

        Assert.Empty(f.Tasks.All);
        Assert.Equal(0, changes.Count);
        Assert.Equal(0, saved);
    }

    [Fact]
    public void Keeps_the_kept_draft_when_the_file_cannot_be_written()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Tasks.KeepDraft(f.Draft("Keep me"));
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.Throws<IOException>(() => f.Tasks.Add("T", "", null));

        Assert.Equal("Keep me", f.Tasks.LoadDraft()!.Title);
    }
}
