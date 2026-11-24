using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Changing a task through <see cref="ITaskStore.Direct"/>, which refuses a change to a task that changed since the caller read it.</summary>
[Unit]
public sealed class DirectUpdateTests
{
    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(5);

    public DirectUpdateTests()
    {
        f.Seed(id, "Original", description: "Body", tags: ["home"]);
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Time.Advance(TimeSpan.FromHours(2));
    }

    private IDirectTaskChanges Direct => f.Store.Direct;

    private static DateTimeOffset Read => DirectChangeChecks.SeededTime;

    [Fact]
    public void Replaces_the_title_description_and_tag_and_sets_updated_to_now()
    {
        var saved = Direct.Update(id, Read, " New title ", "New body", "work");

        Assert.Equal("New title", saved.Title);
        Assert.Equal("New body", saved.Description);
        Assert.Equal(["work"], saved.Tags);
        Assert.Equal(f.Time.GetUtcNow(), saved.Updated);
        TaskAssert.SameTask(saved, Assert.Single(f.Tasks.All));
    }

    [Fact]
    public void Writes_the_change_to_the_tasks_file()
    {
        Direct.Update(id, Read, "Written", null, null);

        using var json = f.ReadJson(f.TaskFile(id));
        Assert.Equal("Written", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public void Keeps_every_field_that_is_left_out()
    {
        var saved = Direct.Update(id, Read, "Only the title", null, null);

        Assert.Equal("Body", saved.Description);
        Assert.Equal(["home"], saved.Tags);
    }

    [Fact]
    public void Keeps_the_title_and_tag_when_only_the_description_is_given()
    {
        var saved = Direct.Update(id, Read, null, "Only the body", null);

        Assert.Equal("Original", saved.Title);
        Assert.Equal(["home"], saved.Tags);
        Assert.Equal("Only the body", saved.Description);
    }

    [Fact]
    public void Clears_the_tag_when_the_tag_is_empty()
    {
        var saved = Direct.Update(id, Read, null, null, "");

        Assert.Empty(saved.Tags);
        Assert.Equal("Original", saved.Title);
    }

    [Fact]
    public void Clears_every_tag_of_an_older_task_that_has_several()
    {
        var older = TasksFixture.Id(6);
        f.Seed(older, "Older", tags: ["one", "two"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = Direct.Update(older, Read, null, null, "");

        Assert.Empty(saved.Tags);
    }

    [Fact]
    public void Stores_description_line_breaks_as_line_feeds()
    {
        var saved = Direct.Update(id, Read, null, "one\r\ntwo", null);

        Assert.Equal("one\ntwo", saved.Description);
    }

    [Fact]
    public void Changes_nothing_else_about_the_task()
    {
        var before = Assert.Single(f.Tasks.All);

        var saved = Direct.Update(id, Read, "Renamed", null, null);

        Assert.Equal(before.Id, saved.Id);
        Assert.Equal(before.Rank, saved.Rank);
        Assert.Equal(before.Created, saved.Created);
        Assert.Equal(before.Resolved, saved.Resolved);
    }

    [Fact]
    public void Raises_changed_once_and_reports_one_local_save()
    {
        var changes = f.CountChanges();
        var saves = f.CountLocalSaves();

        Direct.Update(id, Read, "Announced", null, null);

        Assert.Equal(1, changes.Count);
        Assert.Equal(1, saves.Count);
    }

    [Fact]
    public void Writes_nothing_and_keeps_updated_when_every_given_value_is_the_current_one()
    {
        var before = f.Snapshot();
        var changes = f.CountChanges();
        var saves = f.CountLocalSaves();

        var returned = Direct.Update(id, Read, "Original", "Body", "home");

        Assert.Equal(Read, returned.Updated);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, changes.Count);
        Assert.Equal(0, saves.Count);
    }

    [Fact]
    public void Writes_nothing_when_every_field_is_left_out()
    {
        var before = f.Snapshot();

        var returned = Direct.Update(id, Read, null, null, null);

        Assert.Equal(Read, returned.Updated);
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Throws_stale_and_writes_nothing_when_the_task_changed_since_it_was_read()
    {
        Direct.Update(id, Read, "First change", null, null);
        var before = f.Snapshot();
        var changes = f.CountChanges();

        var stale = Assert.Throws<StaleTaskException>(() => Direct.Update(id, Read, "Second change", null, null));

        TaskAssert.SameTask(Assert.Single(f.Tasks.All), stale.Current);
        Assert.Equal("First change", stale.Current.Title);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, changes.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Throws_stale_when_the_base_time_differs_by_even_a_millisecond(int milliseconds)
    {
        var before = f.Snapshot();

        var stale = Assert.Throws<StaleTaskException>(() => Direct.Update(id, Read.AddMilliseconds(milliseconds), "Nope", null, null));

        Assert.Equal(Read, stale.Current.Updated);
        Assert.Equal("Original", stale.Current.Title);
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Throws_stale_even_when_the_update_would_change_nothing()
    {
        Assert.Throws<StaleTaskException>(() => Direct.Update(id, Read.AddMilliseconds(1), null, null, null));
    }

    [Fact]
    public void Is_a_kind_of_invalid_operation()
    {
        Assert.IsAssignableFrom<InvalidOperationException>(
            Assert.Throws<StaleTaskException>(() => Direct.Update(id, Read.AddDays(1), "Nope", null, null)));
    }

    [Fact]
    public void Accepts_the_updated_time_a_returned_task_carries_for_the_next_change()
    {
        var first = Direct.Update(id, Read, "First", null, null);
        f.Time.Advance(TimeSpan.FromMinutes(1));

        var second = Direct.Update(id, first.Updated, "Second", null, null);

        Assert.Equal("Second", second.Title);
        Assert.Equal(f.Time.GetUtcNow(), second.Updated);
    }

    [Fact]
    public void Throws_not_found_for_an_unknown_task_and_writes_nothing()
    {
        var before = f.Snapshot();

        Assert.Throws<TaskNotFoundException>(() => Direct.Update(TasksFixture.Id(99), Read, "Nope", null, null));

        Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public void Refuses_a_title_that_is_empty_or_has_a_line_break_and_writes_nothing(string title)
    {
        var before = f.Snapshot();

        Assert.ThrowsAny<ArgumentException>(() => Direct.Update(id, Read, title, "other body", null));

        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Refuses_a_tag_with_a_line_break_and_writes_nothing()
    {
        var before = f.Snapshot();

        Assert.ThrowsAny<ArgumentException>(() => Direct.Update(id, Read, "Other", null, "two\nlines"));

        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Refuses_to_update_when_no_folder_is_set()
    {
        f.Tasks.UseRepositoryFolder(null);

        Assert.Throws<NoRepositoryFolderException>(() => Direct.Update(id, Read, "Nowhere", null, null));
    }

    [Fact]
    public void Keeps_the_task_when_the_file_cannot_be_written()
    {
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        var changes = f.CountChanges();

        Assert.Throws<IOException>(() => Direct.Update(id, Read, "Unwritable", null, null));

        Assert.Equal("Original", Assert.Single(f.Tasks.All).Title);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Keeps_the_kept_draft_untouched()
    {
        var draft = f.Draft("Half typed", "text");
        f.Tasks.SaveImage(draft.TaskId, TasksFixture.TinyPng(), "pic");
        f.Tasks.KeepDraft(draft);
        var deviceBefore = f.DeviceSnapshot();

        Direct.Update(id, Read, "Direct", "changed", "work");

        f.AssertDraftKept(draft);
        Assert.Equal(deviceBefore, f.DeviceSnapshot());
    }

    [Fact]
    public void Keeps_the_waiting_images_of_an_open_edit_of_the_same_task()
    {
        f.Tasks.SaveImage(id, TasksFixture.TinyPng(), "pic");
        var deviceBefore = f.DeviceSnapshot();

        Direct.Update(id, Read, "Direct", "changed", "work");

        Assert.Equal(deviceBefore, f.DeviceSnapshot());
    }
}
