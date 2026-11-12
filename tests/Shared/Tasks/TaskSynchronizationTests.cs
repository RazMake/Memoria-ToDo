using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Sync's access to Tasks: skipped files, automatic deletion, and exclusive local updates.</summary>
[Unit]
public sealed class TaskSynchronizationTests
{
    // The fixture's clock reads 2026-11-12T08:30:15.123Z.
    private const string ExactlyAWeekAgo = "2026-11-05T08:30:15.123Z";
    private const string AMillisecondOlder = "2026-11-05T08:30:15.122Z";
    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(1);

    private ITaskSynchronization Sync => f.Store.Synchronization;

    private Counts Count()
    {
        var counts = new Counts();
        f.Tasks.Changed += (_, _) => counts.Changed++;
        f.Store.Settings.Changed += (_, _) => counts.SettingsChanged++;
        Sync.LocalSaved += (_, _) => counts.Saved++;
        return counts;
    }

    private sealed class Counts
    {
        public int Changed { get; set; }
        public int SettingsChanged { get; set; }
        public int Saved { get; set; }
    }

    // Selected folder and skipped files

    [Fact]
    public void Reports_the_selected_folder_or_none()
    {
        Assert.Null(Sync.RepositoryFolder);

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(f.Clone, Sync.RepositoryFolder);
    }

    [Fact]
    public void Skips_corrupt_and_newer_files_by_name_and_keeps_their_bytes_and_the_readable_tasks()
    {
        var corrupt = TasksFixture.Id(2);
        var newer = TasksFixture.Id(3);
        f.Seed(id, "Readable");
        f.WriteText(f.TaskFile(corrupt), "{ not json");
        f.Seed(newer, "Newer", version: 99);
        f.WriteText(f.SettingsFile, "{ not json");
        var newerBytes = f.Files.ReadFile(f.TaskFile(newer));

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Readable"], f.Titles);
        var skipped = Sync.SkippedFiles.ToDictionary(s => s.RelativePath);
        Assert.Equal(3, skipped.Count);
        Assert.False(skipped[$"tasks/{corrupt}.json"].RequiresUpdate);
        Assert.True(skipped[$"tasks/{newer}.json"].RequiresUpdate);
        Assert.False(skipped["settings.json"].RequiresUpdate);
        Assert.All(skipped.Values, s => Assert.False(string.IsNullOrWhiteSpace(s.Reason)));
        Assert.Equal("{ not json", f.ReadText(f.TaskFile(corrupt)));
        Assert.Equal(newerBytes, f.Files.ReadFile(f.TaskFile(newer)));
        Assert.Equal("{ not json", f.ReadText(f.SettingsFile));
    }

    [Theory]
    [InlineData("notes.json")]
    [InlineData("01900000-0000-7000-8000-000000000004.json")]
    [InlineData("01900000-0000-7000-8000-000000000005.JSON")]
    public void Does_not_load_a_task_file_whose_name_is_not_its_task_id_and_never_deletes_for_it(string fileName)
    {
        var payloadId = TasksFixture.Id(5);
        f.Seed(payloadId, "Misnamed", resolved: "2020-01-01T00:00:00.000Z");
        var path = Path.Combine(Path.GetDirectoryName(f.TaskFile(payloadId))!, fileName);
        var bytes = f.Files.ReadFile(f.TaskFile(payloadId));
        f.Files.DeleteFile(f.TaskFile(payloadId));
        f.Files.WriteFile(path, bytes);
        f.Seed(id, "Open");
        f.Tasks.UseRepositoryFolder(f.Clone);
        var counts = Count();

        Sync.DeleteExpiredTasks();
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Open"], f.Titles);
        Assert.Equal(0, counts.Saved);
        Assert.Equal(bytes, f.Files.ReadFile(path));
        Assert.False(f.Files.FileExists(f.TaskFile(payloadId)));
    }

    [Fact]
    public void Skips_a_newer_settings_file_and_asks_for_an_update()
    {
        f.SeedSettings(version: 2);

        f.Tasks.UseRepositoryFolder(f.Clone);

        var file = Assert.Single(Sync.SkippedFiles);
        Assert.Equal("settings.json", file.RelativePath);
        Assert.True(file.RequiresUpdate);
    }

    [Fact]
    public void Reports_no_skipped_files_when_everything_reads()
    {
        f.Seed(id);
        f.SeedSettings();

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Empty(Sync.SkippedFiles);
    }

    // Automatic deletion

    [Fact]
    public void Deletes_a_task_resolved_longer_ago_than_the_default_week_but_not_exactly_a_week()
    {
        f.Seed(id, "Exactly", resolved: ExactlyAWeekAgo);
        f.Seed(TasksFixture.Id(2), "Older", resolved: AMillisecondOlder);
        f.Seed(TasksFixture.Id(3), "Open");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Sync.DeleteExpiredTasks();

        Assert.Equal(["Exactly", "Open"], f.Titles.Order().ToList());
        Assert.False(f.Files.FileExists(f.TaskFile(TasksFixture.Id(2))));
    }

    [Fact]
    public void Uses_the_shared_delay_even_for_a_task_edited_since_it_was_resolved()
    {
        f.SeedSettings(days: 2);
        f.Seed(id, "Edited later", resolved: "2026-11-09T08:30:15.123Z", updated: "2026-11-12T00:00:00.000Z");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Sync.DeleteExpiredTasks();

        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Deletes_the_images_of_an_expired_task()
    {
        f.Seed(id, resolved: AMillisecondOlder);
        f.WriteText(Path.Combine(f.CloneImages(id), "pic.png"), "png");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Sync.DeleteExpiredTasks();

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
    }

    [Fact]
    public void Deletes_nothing_and_throws_when_the_settings_cannot_be_read()
    {
        f.Seed(id, resolved: "2020-01-01T00:00:00.000Z");
        f.WriteText(f.SettingsFile, "{ not json");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Throws<InvalidDataException>(Sync.DeleteExpiredTasks);

        Assert.Single(f.Tasks.All);
        Assert.True(f.Files.FileExists(f.TaskFile(id)));
    }

    [Fact]
    public void Refuses_to_delete_when_no_repository_is_selected()
    {
        Assert.Throws<NoRepositoryFolderException>(Sync.DeleteExpiredTasks);
    }

    [Fact]
    public void Reports_one_local_save_only_when_an_expired_task_was_deleted()
    {
        f.Seed(id, resolved: AMillisecondOlder);
        f.Tasks.UseRepositoryFolder(f.Clone);
        var counts = Count();

        Sync.DeleteExpiredTasks();
        Assert.Equal(1, counts.Saved);

        Sync.DeleteExpiredTasks();
        Assert.Equal(1, counts.Saved);
    }

    // Exclusive updates

    [Fact]
    public void Refuses_an_update_for_a_folder_that_is_no_longer_selected_before_running_it()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => Sync.UpdateRepository(Path.Combine(f.Clone, "other"), () => ran = true));
        f.Tasks.UseRepositoryFolder(null);
        Assert.Throws<InvalidOperationException>(() => Sync.UpdateRepository(f.Clone, () => ran = true));

        Assert.False(ran);
    }

    [Fact]
    public void Reloads_what_the_update_changed_and_raises_notices_without_a_local_save()
    {
        f.Seed(id, "Before");
        f.Tasks.UseRepositoryFolder(f.Clone);
        var counts = Count();

        Sync.UpdateRepository(f.Clone, () =>
        {
            f.Seed(id, "Imported");
            f.Seed(TasksFixture.Id(2), "New");
            Assert.Equal(0, counts.Changed);
        });

        Assert.Equal(["Imported", "New"], f.Titles.Order().ToList());
        Assert.Equal(1, counts.Changed);
        Assert.Equal(0, counts.Saved);
    }

    [Fact]
    public void Refreshes_skipped_files_after_an_update()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Sync.UpdateRepository(f.Clone, () => f.WriteText(f.TaskFile(id), "{ not json"));

        Assert.Equal($"tasks/{id}.json", Assert.Single(Sync.SkippedFiles).RelativePath);
    }

    [Fact]
    public void Raises_notices_after_the_lock_is_released()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var otherThreadFinished = false;
        f.Tasks.Changed += (_, _) =>
            otherThreadFinished = Task.Run(() => f.Tasks.All.Count).Wait(TimeSpan.FromSeconds(10));

        Sync.UpdateRepository(f.Clone, () => f.Seed(id));

        Assert.True(otherThreadFinished);
    }

    [Fact]
    public async Task Lets_another_thread_save_locally_only_after_the_update_and_keeps_that_save()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var counts = Count();
        var started = new ManualResetEventSlim();
        Task<TaskItem>? add = null;

        Sync.UpdateRepository(f.Clone, () =>
        {
            add = Task.Run(() =>
            {
                started.Set();
                return f.Tasks.Add(f.Draft("Concurrent"));
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        });

        await add!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(["Concurrent"], f.Titles);
        Assert.Equal(1, counts.Saved);
    }

    [Fact]
    public void Reloads_and_raises_notices_then_rethrows_when_the_update_fails()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var counts = Count();
        var failure = new InvalidOperationException("merge failed");

        var thrown = Assert.Throws<InvalidOperationException>(() => Sync.UpdateRepository(f.Clone, () =>
        {
            f.Seed(id, "Half applied");
            throw failure;
        }));

        Assert.Same(failure, thrown);
        Assert.Equal(["Half applied"], f.Titles);
        Assert.Equal(1, counts.Changed);
        Assert.Equal(0, counts.Saved);
    }

    [Fact]
    public void Keeps_both_failures_when_the_update_and_the_reload_fail()
    {
        f.Seed(id, "Existing");
        f.Tasks.UseRepositoryFolder(f.Clone);
        var failure = new InvalidOperationException("merge failed");

        var thrown = Assert.Throws<AggregateException>(() => Sync.UpdateRepository(f.Clone, () =>
        {
            // A file where the tasks folder should be stops the whole reload, not just one file.
            f.Files.WriteFile(Path.Combine(f.Clone, "tasks"), [1]);
            throw failure;
        }));

        Assert.Contains(failure, thrown.InnerExceptions);
        Assert.Equal(2, thrown.InnerExceptions.Count);
    }

    [Fact]
    public void Skips_a_task_file_that_cannot_be_read_when_reloading_after_an_update()
    {
        f.Seed(id, "Existing");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Sync.UpdateRepository(f.Clone, () => f.Files.FailReadsUnder(f.Clone));

        Assert.Empty(f.Titles);
        Assert.Equal($"tasks/{id}.json", Assert.Single(Sync.SkippedFiles).RelativePath);
    }
}
