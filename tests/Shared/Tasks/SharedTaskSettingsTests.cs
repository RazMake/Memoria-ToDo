using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>The three settings every device shares, kept in settings.json.</summary>
[Unit]
public sealed class SharedTaskSettingsTests
{
    private const string Epoch = "1970-01-01T00:00:00.000Z";
    private const string NowText = "2026-11-12T08:30:15.123Z";
    private readonly TasksFixture f = new();

    private ISharedTaskSettings Settings => f.Store.Settings;

    private (Func<int> Settings, Func<int> Saved) CountNotices()
    {
        var settings = 0;
        var saved = 0;
        Settings.Changed += (_, _) => settings++;
        f.Store.Synchronization.LocalSaved += (_, _) => saved++;
        return (() => settings, () => saved);
    }

    private (int Value, string Updated) Read(string setting)
    {
        using var json = f.ReadJson(f.SettingsFile);
        var node = json.RootElement.GetProperty(setting);
        return (node.GetProperty("value").ValueKind == System.Text.Json.JsonValueKind.Number ? node.GetProperty("value").GetInt32() : 0,
            node.GetProperty("updated").GetString()!);
    }

    private string? ReadShortcut(string setting)
    {
        using var json = f.ReadJson(f.SettingsFile);
        return json.RootElement.GetProperty(setting).GetProperty("value").GetString();
    }

    // Reading

    [Fact]
    public void Supplies_a_week_and_no_shortcuts_without_a_file_and_writes_nothing()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(7, Settings.DeletionDelayDays);
        Assert.Null(Settings.AddNewTaskShortcut);
        Assert.Null(Settings.ManageTasksShortcut);
        Assert.False(f.Files.FileExists(f.SettingsFile));
    }

    [Fact]
    public void Supplies_the_defaults_when_no_repository_is_selected()
    {
        Assert.Equal(7, Settings.DeletionDelayDays);
        Assert.Null(Settings.AddNewTaskShortcut);
    }

    [Fact]
    public void Reads_the_values_in_the_file()
    {
        f.SeedSettings(days: 30, add: "Ctrl+Alt+N", manage: "Ctrl+Alt+M");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(30, Settings.DeletionDelayDays);
        Assert.Equal("Ctrl+Alt+N", Settings.AddNewTaskShortcut);
        Assert.Equal("Ctrl+Alt+M", Settings.ManageTasksShortcut);
    }

    // Writing

    [Fact]
    public void Saves_a_delay_with_the_current_time_and_keeps_the_other_settings_times()
    {
        f.SeedSettings(days: 7, add: "A", manage: "M", addTime: "2026-02-02T00:00:00.000Z", manageTime: "2026-03-03T00:00:00.000Z");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Settings.SetDeletionDelayDays(30);

        Assert.Equal((30, NowText), Read("deletionDelayDays"));
        Assert.Equal("2026-02-02T00:00:00.000Z", Read("addNewTaskShortcut").Updated);
        Assert.Equal("2026-03-03T00:00:00.000Z", Read("manageTasksShortcut").Updated);
        Assert.Equal(30, Settings.DeletionDelayDays);
    }

    [Fact]
    public void Creates_a_missing_file_changing_only_the_set_settings_time()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Settings.SetAddNewTaskShortcut("Win+Shift+T");

        Assert.Equal("Win+Shift+T", ReadShortcut("addNewTaskShortcut"));
        Assert.Equal(NowText, Read("addNewTaskShortcut").Updated);
        Assert.Equal((7, Epoch), Read("deletionDelayDays"));
        Assert.Equal(Epoch, Read("manageTasksShortcut").Updated);
    }

    [Fact]
    public void Keeps_shortcut_text_as_typed_and_lets_null_restore_the_default()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Settings.SetManageTasksShortcut("  not a shortcut  ");
        Assert.Equal("  not a shortcut  ", Settings.ManageTasksShortcut);
        Assert.Equal(NowText, Read("manageTasksShortcut").Updated);
        Settings.SetAddNewTaskShortcut(" typed ");
        Assert.Equal(" typed ", Settings.AddNewTaskShortcut);

        f.Time.Advance(TimeSpan.FromMinutes(1));
        Settings.SetManageTasksShortcut(null);
        Assert.Null(Settings.ManageTasksShortcut);
        Assert.Null(ReadShortcut("manageTasksShortcut"));
    }

    [Fact]
    public void Keeps_the_settings_after_a_restart()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        Settings.SetDeletionDelayDays(90);

        var restarted = Assert.IsAssignableFrom<ITaskStore>(f.Restarted());
        restarted.UseRepositoryFolder(f.Clone);

        Assert.Equal(90, restarted.Settings.DeletionDelayDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void Refuses_a_delay_outside_1_to_365_and_writes_nothing(int days)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Throws<ArgumentOutOfRangeException>(() => Settings.SetDeletionDelayDays(days));

        Assert.False(f.Files.FileExists(f.SettingsFile));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public void Accepts_the_ends_of_the_delay_range(int days)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Settings.SetDeletionDelayDays(days);

        Assert.Equal(days, Settings.DeletionDelayDays);
    }

    [Fact]
    public void Refuses_every_setter_when_no_repository_is_selected()
    {
        Assert.Throws<NoRepositoryFolderException>(() => Settings.SetDeletionDelayDays(3));
        Assert.Throws<NoRepositoryFolderException>(() => Settings.SetAddNewTaskShortcut("x"));
        Assert.Throws<NoRepositoryFolderException>(() => Settings.SetManageTasksShortcut("x"));
    }

    [Fact]
    public void Writes_nothing_and_raises_no_notice_when_a_setter_keeps_the_value()
    {
        f.SeedSettings(days: 7, add: "A");
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Files.ReadFile(f.SettingsFile);
        var (settings, saved) = CountNotices();
        f.Files.FailWritesUnder(f.Clone);

        Settings.SetDeletionDelayDays(7);
        Settings.SetAddNewTaskShortcut("A");
        Settings.SetManageTasksShortcut(null);

        Assert.Equal(before, f.Files.ReadFile(f.SettingsFile));
        Assert.Equal(0, settings());
        Assert.Equal(0, saved());
    }

    // Unreadable or newer files

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "version": 1, "deletionDelayDays": { "value": 7, "updated": "2026-01-01T00:00:00.000Z" } }""")]
    public void Fails_reads_and_writes_on_an_unreadable_file_and_leaves_it_untouched(string content)
    {
        f.WriteText(f.SettingsFile, content);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Throws<InvalidDataException>(() => Settings.DeletionDelayDays);
        Assert.Throws<InvalidDataException>(() => Settings.AddNewTaskShortcut);
        Assert.Throws<InvalidDataException>(() => Settings.SetDeletionDelayDays(3));
        Assert.Throws<InvalidDataException>(() => Settings.SetManageTasksShortcut("x"));
        Assert.Throws<InvalidDataException>(f.Store.Synchronization.EnsureSettings);
        Assert.Equal(content, f.ReadText(f.SettingsFile));
    }

    [Fact]
    public void Fails_on_a_newer_version_and_leaves_the_file_untouched()
    {
        var newer = TasksFixture.SettingsJson(version: 2);
        f.WriteText(f.SettingsFile, newer);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Throws<InvalidDataException>(() => Settings.DeletionDelayDays);
        Assert.Throws<InvalidDataException>(() => Settings.SetDeletionDelayDays(3));
        Assert.Equal(newer, f.ReadText(f.SettingsFile));
    }

    [Fact]
    public void Still_loads_tasks_when_the_settings_file_is_unreadable()
    {
        f.WriteText(f.SettingsFile, "{ not json");
        f.Seed(TasksFixture.Id(1), "Readable");

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Readable"], f.Titles);
    }

    // A settings file that can't be probed (File.Exists says false after an I/O or access error) is not a missing file

    private (ITaskStore Store, UnprobeableFileSystem Files) OpenWithSettingsFileHidden()
    {
        var files = new UnprobeableFileSystem(f.Files, f.SettingsFile);
        var store = Assert.IsAssignableFrom<ITaskStore>(TaskStore.Create(files, f.Encoder, f.Time, f.Device));
        store.UseRepositoryFolder(f.Clone);
        return (store, files);
    }

    [Fact]
    public void Fails_reads_writes_and_initialization_when_the_settings_file_cannot_be_probed_and_leaves_it_untouched()
    {
        f.SeedSettings(days: 30, add: "A");
        var before = f.Files.ReadFile(f.SettingsFile);
        var (store, files) = OpenWithSettingsFileHidden();
        var writes = files.Writes;

        Assert.Throws<InvalidDataException>(() => store.Settings.DeletionDelayDays);
        Assert.Throws<InvalidDataException>(() => store.Settings.AddNewTaskShortcut);
        Assert.Throws<InvalidDataException>(() => store.Settings.SetDeletionDelayDays(3));
        Assert.Throws<InvalidDataException>(() => store.Settings.SetManageTasksShortcut("x"));
        Assert.Throws<InvalidDataException>(store.Synchronization.EnsureSettings);
        Assert.Equal(before, f.Files.ReadFile(f.SettingsFile));
        Assert.Equal(writes, files.Writes);
    }

    [Fact]
    public void Deletes_no_task_or_image_when_the_settings_file_cannot_be_probed()
    {
        // A delay of 30 days keeps a task resolved 14 days ago; the default week would not.
        f.SeedSettings(days: 30);
        f.Seed(TasksFixture.Id(1), "Resolved", resolved: "2026-10-29T08:30:15.123Z");
        f.WriteText(Path.Combine(f.CloneImages(TasksFixture.Id(1)), "pic.png"), "png");
        var (store, _) = OpenWithSettingsFileHidden();

        Assert.Throws<InvalidDataException>(store.Synchronization.DeleteExpiredTasks);

        Assert.Equal(["Resolved"], store.All.Select(t => t.Title).ToList());
        Assert.True(f.Files.FileExists(f.TaskFile(TasksFixture.Id(1))));
        Assert.Single(f.Files.AllFilesUnder(f.CloneImages(TasksFixture.Id(1))));
    }

    // Initialization

    [Fact]
    public void Initialization_creates_the_defaults_dated_at_the_epoch()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Store.Synchronization.EnsureSettings();

        Assert.Equal((7, Epoch), Read("deletionDelayDays"));
        Assert.Null(ReadShortcut("addNewTaskShortcut"));
        Assert.Equal(Epoch, Read("addNewTaskShortcut").Updated);
        Assert.Equal(Epoch, Read("manageTasksShortcut").Updated);
    }

    [Fact]
    public void Initialization_leaves_an_existing_file_as_it_is()
    {
        f.SeedSettings(days: 30, add: "A");
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Files.ReadFile(f.SettingsFile);

        f.Store.Synchronization.EnsureSettings();

        Assert.Equal(before, f.Files.ReadFile(f.SettingsFile));
    }

    [Fact]
    public void Initialization_fails_when_no_repository_is_selected()
    {
        Assert.Throws<NoRepositoryFolderException>(f.Store.Synchronization.EnsureSettings);
    }

    // Notices

    [Fact]
    public void Raises_one_settings_notice_for_a_change_and_one_local_save()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (settings, saved) = CountNotices();

        Settings.SetDeletionDelayDays(30);

        Assert.Equal(1, settings());
        Assert.Equal(1, saved());
    }

    [Fact]
    public void Raises_a_settings_notice_but_no_local_save_when_a_repository_is_selected()
    {
        f.SeedSettings(days: 30);
        var (settings, saved) = CountNotices();

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(1, settings());
        Assert.Equal(0, saved());
    }

    [Fact]
    public void Raises_a_settings_notice_but_no_local_save_when_an_update_reloads_the_settings()
    {
        f.SeedSettings(days: 30);
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (settings, saved) = CountNotices();

        f.Store.Synchronization.UpdateRepository(f.Clone, () => f.SeedSettings(days: 60));

        Assert.Equal(60, Settings.DeletionDelayDays);
        Assert.Equal(1, settings());
        Assert.Equal(0, saved());
    }

    // Local saves

    [Fact]
    public void Reports_a_local_save_for_a_task_change_but_not_for_a_draft_or_an_image_waiting_outside_the_clone()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (_, saved) = CountNotices();

        var draft = f.Tasks.StartDraft();
        f.Tasks.KeepDraft(draft with { Title = "Kept" });
        f.Tasks.SaveImage(draft.TaskId, Fakes.FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200), "alt");
        f.Tasks.DiscardDraft();
        Assert.Equal(0, saved());

        f.Tasks.Add(f.Draft("Added"));
        Assert.Equal(1, saved());
    }
}
