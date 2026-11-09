using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Creating Tasks, and reading the clone's task files into the list.</summary>
[Unit]
public sealed class ClonePickupTests
{
    private readonly TasksFixture f = new();

    [Fact]
    public void Refuses_a_null_file_system()
    {
        Assert.Throws<ArgumentNullException>(() => TaskStore.Create(null!, f.Encoder, f.Time, f.Device));
    }

    [Fact]
    public void Refuses_a_null_image_encoder()
    {
        Assert.Throws<ArgumentNullException>(() => TaskStore.Create(f.Files, null!, f.Time, f.Device));
    }

    [Fact]
    public void Refuses_a_null_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => TaskStore.Create(f.Files, f.Encoder, null!, f.Device));
    }

    [Fact]
    public void Refuses_a_null_device_folder()
    {
        Assert.Throws<ArgumentNullException>(() => TaskStore.Create(f.Files, f.Encoder, f.Time, null!));
    }

    [Fact]
    public void Refuses_an_empty_device_folder()
    {
        Assert.ThrowsAny<ArgumentException>(() => TaskStore.Create(f.Files, f.Encoder, f.Time, ""));
    }

    [Fact]
    public void Has_no_tasks_before_a_folder_is_given()
    {
        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Lists_the_tasks_in_the_folders_tasks_folder()
    {
        f.Seed(TasksFixture.Id(1), title: "First", rank: "a0");
        f.Seed(TasksFixture.Id(2), title: "Second", rank: "a1");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["First", "Second"], f.Titles);
    }

    [Fact]
    public void Reads_every_field_of_a_task_file()
    {
        f.Seed(TasksFixture.Id(1), title: "Pay rent", description: "Line one\nLine two", rank: "a5",
            tags: ["home", "money"], resolved: "2026-03-04T05:06:07.890Z",
            created: "2026-01-02T03:04:05.006Z", updated: "2026-02-03T04:05:06.007Z");

        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = Assert.Single(f.Tasks.All);
        Assert.Equal(TasksFixture.Id(1), task.Id);
        Assert.Equal("Pay rent", task.Title);
        Assert.Equal("Line one\nLine two", task.Description);
        Assert.Equal("a5", task.Rank);
        Assert.Equal(["home", "money"], task.Tags);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, TimeSpan.Zero), task.Created);
        Assert.Equal(new DateTimeOffset(2026, 2, 3, 4, 5, 6, 7, TimeSpan.Zero), task.Updated);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 5, 6, 7, 890, TimeSpan.Zero), task.Resolved);
    }

    [Fact]
    public void Reads_an_open_task_as_not_resolved()
    {
        f.Seed(TasksFixture.Id(1), resolved: null);

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Null(Assert.Single(f.Tasks.All).Resolved);
    }

    [Fact]
    public void Lists_resolved_tasks_with_the_open_ones()
    {
        f.Seed(TasksFixture.Id(1), title: "Open", rank: "a0");
        f.Seed(TasksFixture.Id(2), title: "Done", rank: "a1", resolved: "2026-03-04T05:06:07.890Z");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Open", "Done"], f.Titles);
    }

    [Fact]
    public void Ignores_files_in_the_tasks_folder_that_are_not_json()
    {
        f.Seed(TasksFixture.Id(1), title: "Real");
        f.WriteText(Path.Combine(f.Clone, "tasks", "notes.txt"), "not a task");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Real"], f.Titles);
    }

    [Fact]
    public void Raises_changed_once_after_reading_the_folder()
    {
        f.Seed(TasksFixture.Id(1));
        f.Seed(TasksFixture.Id(2), rank: "a1");
        var changes = f.CountChanges();

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Has_the_tasks_already_in_the_list_when_changed_is_raised()
    {
        f.Seed(TasksFixture.Id(1));
        var seen = -1;
        f.Tasks.Changed += (_, _) => seen = f.Tasks.All.Count;

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(1, seen);
    }

    [Fact]
    public void Empties_the_list_and_raises_changed_when_the_folder_is_cleared()
    {
        f.Seed(TasksFixture.Id(1));
        f.Tasks.UseRepositoryFolder(f.Clone);
        var changes = f.CountChanges();

        f.Tasks.UseRepositoryFolder(null);

        Assert.Empty(f.Tasks.All);
        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Leaves_the_files_when_the_folder_is_cleared()
    {
        f.Seed(TasksFixture.Id(1));
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.UseRepositoryFolder(null);

        Assert.True(f.Files.FileExists(f.TaskFile(TasksFixture.Id(1))));
    }

    [Fact]
    public void Switches_to_the_tasks_of_another_folder()
    {
        var other = Path.Combine(Path.GetTempPath(), "memoria-tasks-tests", "other-clone");
        f.Seed(TasksFixture.Id(1), title: "In the first clone");
        f.WriteText(Path.Combine(other, "tasks", TasksFixture.Id(2) + ".json"),
            """{ "id": "01900000-0000-7000-8000-000000000002", "title": "In the second clone", "description": "", "rank": "a0", "tags": [], "created": "2026-01-01T00:00:00.000Z", "updated": "2026-01-01T00:00:00.000Z", "resolved": null, "version": 1 }""");
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.UseRepositoryFolder(other);

        Assert.Equal(["In the second clone"], f.Titles);
    }

    [Fact]
    public void Has_no_tasks_when_the_folder_has_no_tasks_folder()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Skips_a_task_file_that_cannot_be_read_and_lists_the_rest()
    {
        f.Seed(TasksFixture.Id(1), title: "Good");
        f.WriteText(f.TaskFile(TasksFixture.Id(2)), "{ this is not json");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Good"], f.Titles);
    }

    [Fact]
    public void Leaves_a_task_file_that_cannot_be_read_unchanged()
    {
        f.WriteText(f.TaskFile(TasksFixture.Id(2)), "{ this is not json");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal("{ this is not json", f.ReadText(f.TaskFile(TasksFixture.Id(2))));
    }

    [Fact]
    public void Skips_a_task_file_that_lacks_required_fields()
    {
        f.WriteText(f.TaskFile(TasksFixture.Id(2)), """{ "id": "01900000-0000-7000-8000-000000000002" }""");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Skips_a_task_file_with_a_newer_version_and_leaves_it_unchanged()
    {
        f.Seed(TasksFixture.Id(1), title: "Current");
        f.Seed(TasksFixture.Id(2), title: "From the future", version: 2);
        var before = f.Files.ReadFile(f.TaskFile(TasksFixture.Id(2)));

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Current"], f.Titles);
        Assert.Equal(before, f.Files.ReadFile(f.TaskFile(TasksFixture.Id(2))));
    }

    [Fact]
    public void Orders_tasks_by_rank_character_by_character_in_code_order()
    {
        f.Seed(TasksFixture.Id(1), title: "small a", rank: "a");
        f.Seed(TasksFixture.Id(2), title: "capital Z", rank: "Z");
        f.Seed(TasksFixture.Id(3), title: "digit 0", rank: "0");
        f.Seed(TasksFixture.Id(4), title: "capital B", rank: "B");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["digit 0", "capital B", "capital Z", "small a"], f.Titles);
    }

    [Fact]
    public void Orders_a_key_before_a_longer_key_it_starts()
    {
        f.Seed(TasksFixture.Id(1), title: "longer", rank: "a0V");
        f.Seed(TasksFixture.Id(2), title: "shorter", rank: "a0");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["shorter", "longer"], f.Titles);
    }

    [Fact]
    public void Orders_tasks_with_equal_ranks_by_id()
    {
        f.Seed(TasksFixture.Id(3), title: "third", rank: "a0");
        f.Seed(TasksFixture.Id(1), title: "first", rank: "a0");
        f.Seed(TasksFixture.Id(2), title: "second", rank: "a0");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["first", "second", "third"], f.Titles);
    }

    [Fact]
    public async Task Raises_changed_on_the_thread_that_gave_the_folder()
    {
        f.Seed(TasksFixture.Id(1));
        int? raisedOn = null;
        f.Tasks.Changed += (_, _) => raisedOn = Environment.CurrentManagedThreadId;

        var calledOn = await Task.Run(() =>
        {
            f.Tasks.UseRepositoryFolder(f.Clone);
            return Environment.CurrentManagedThreadId;
        });

        Assert.Equal(calledOn, raisedOn);
    }

    [Fact]
    public async Task Raises_changed_on_the_thread_that_added_the_task()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        int? raisedOn = null;
        f.Tasks.Changed += (_, _) => raisedOn = Environment.CurrentManagedThreadId;

        var calledOn = await Task.Run(() =>
        {
            f.Tasks.Add(f.Draft());
            return Environment.CurrentManagedThreadId;
        });

        Assert.Equal(calledOn, raisedOn);
    }

    [Fact]
    public void Adds_every_task_when_called_from_many_threads_at_once()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var drafts = Enumerable.Range(0, 40).Select(_ => f.Draft()).ToList();

        Parallel.ForEach(drafts, draft => f.Tasks.Add(draft));

        Assert.Equal(drafts.Select(d => d.TaskId).Order(StringComparer.Ordinal), f.Ids.Order(StringComparer.Ordinal));
        Assert.Equal(40, f.Tasks.All.Select(t => t.Rank).Distinct().Count());
    }
}
