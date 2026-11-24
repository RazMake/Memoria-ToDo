using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Adding a task through <see cref="ITaskStore.Direct"/>, for a caller with no editor.</summary>
[Unit]
public sealed class DirectAddTests
{
    private readonly TasksFixture f = new();

    private IDirectTaskChanges Direct => f.Store.Direct;

    [Fact]
    public void Adds_an_open_task_with_the_given_title_description_and_tag()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = Direct.Add("  Buy milk  ", "Two liters", "home");

        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("Two liters", task.Description);
        Assert.Equal(["home"], task.Tags);
        Assert.Null(task.Resolved);
        Assert.True(Guid.TryParseExact(task.Id, "D", out _));
        TaskAssert.SameTask(task, Assert.Single(f.Tasks.All));
    }

    [Fact]
    public void Adds_a_task_with_no_tag_when_the_tag_is_null()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Empty(Direct.Add("No tag", "", null).Tags);
    }

    [Fact]
    public void Sets_created_and_updated_to_now()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = Direct.Add("Timed", "", null);

        Assert.Equal(TasksFixture.Start, task.Created);
        Assert.Equal(TasksFixture.Start, task.Updated);
    }

    [Fact]
    public void Puts_the_new_task_above_the_existing_ones()
    {
        f.SeedAndUse(TasksFixture.Id(1), "Old", rank: "a0");

        var task = Direct.Add("New", "", null);

        Assert.Equal([task.Id, TasksFixture.Id(1)], f.Ids);
        Assert.True(string.CompareOrdinal(task.Rank, "a0") < 0, task.Rank);
    }

    [Fact]
    public void Gives_each_new_task_its_own_id()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var first = Direct.Add("One", "", null);
        var second = Direct.Add("Two", "", null);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Writes_the_tasks_file_and_stores_line_breaks_as_line_feeds()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = Direct.Add("Stored", "one\r\ntwo", null);

        Assert.Equal("one\ntwo", task.Description);
        using var json = f.ReadJson(f.TaskFile(task.Id));
        Assert.Equal("Stored", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("one\ntwo", json.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public void Raises_changed_once_and_reports_one_local_save()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var changes = f.CountChanges();
        var saves = f.CountLocalSaves();

        Direct.Add("Announced", "", null);

        Assert.Equal(1, changes.Count);
        Assert.Equal(1, saves.Count);
    }

    [Fact]
    public void Trims_the_tag()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["work"], Direct.Add("T", "", "  work ").Tags);
    }

    [Fact]
    public void Keeps_the_task_after_the_app_restarts()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var task = Direct.Add("Persistent", "body", "tag");

        var again = f.Restarted();
        again.UseRepositoryFolder(f.Clone);

        TaskAssert.SameTask(task, Assert.Single(again.All));
    }

    [Fact]
    public void Writes_nothing_but_the_task_file_into_the_clone()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = Direct.Add("Alone", "", null);

        Assert.Equal([f.TaskFile(task.Id)], f.Files.AllFilesUnder(f.Clone));
    }

    [Fact]
    public void Logs_one_information_entry_naming_the_task_id_and_title()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Logs.Events.Count;

        var task = Direct.Add("Buy milk", "", null);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, task.Id, "Buy milk");
    }

    [Fact]
    public void Keeps_the_kept_draft_and_its_images_untouched()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Half typed", "text");
        f.Tasks.SaveImage(draft.TaskId, TasksFixture.TinyPng(), "pic");
        f.Tasks.KeepDraft(draft);
        var draftFilesBefore = f.DeviceSnapshot();

        Direct.Add("Direct", "", null);

        f.AssertDraftKept(draft);
        Assert.Equal(draftFilesBefore, f.DeviceSnapshot());
    }

    [Fact]
    public void Keeps_the_waiting_images_of_an_open_edit()
    {
        var edited = TasksFixture.Id(7);
        f.SeedAndUse(edited, "Existing");
        f.Tasks.SaveImage(edited, TasksFixture.TinyPng(), "pic");
        var deviceBefore = f.DeviceSnapshot();

        Direct.Add("Direct", "", null);

        Assert.Equal(deviceBefore, f.DeviceSnapshot());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    [InlineData("two\r\nlines")]
    public void Refuses_a_title_that_is_empty_or_has_a_line_break_and_writes_nothing(string title)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Snapshot();

        Assert.ThrowsAny<ArgumentException>(() => Direct.Add(title, "", null));

        Assert.Equal(before, f.Snapshot());
        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Adds_a_task_with_no_tag_when_the_tag_is_empty()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Empty(Direct.Add("Empty tag", "", "").Tags);
    }

    [Fact]
    public void Refuses_a_tag_that_is_only_whitespace_and_writes_nothing()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Snapshot();

        Assert.ThrowsAny<ArgumentException>(() => Direct.Add("Title", "", "   "));

        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Refuses_a_tag_with_a_line_break_and_writes_nothing()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Snapshot();

        Assert.ThrowsAny<ArgumentException>(() => Direct.Add("Title", "", "two\nlines"));

        Assert.Equal(before, f.Snapshot());
        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Refuses_to_add_when_no_folder_is_set_and_writes_nothing()
    {
        Assert.Throws<NoRepositoryFolderException>(() => Direct.Add("Nowhere", "", null));

        Assert.Empty(f.Files.AllFilesUnder(f.Clone));
    }

    [Fact]
    public void Adds_nothing_when_the_file_cannot_be_written()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        var changes = f.CountChanges();

        Assert.Throws<IOException>(() => Direct.Add("Unwritable", "", null));

        Assert.Empty(f.Tasks.All);
        Assert.Equal(0, changes.Count);
    }
}
