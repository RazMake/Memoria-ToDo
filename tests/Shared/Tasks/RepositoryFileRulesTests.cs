using System.Text;
using System.Text.Json;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Tasks' managed repository paths and the pure three-version merges of task and settings files.</summary>
[Unit]
public sealed class RepositoryFileRulesTests
{
    private const string T0 = "2026-01-01T00:00:00.000Z";
    private const string T1 = "2026-02-01T00:00:00.000Z";
    private const string T2 = "2026-03-01T00:00:00.000Z";
    private const string T3 = "2026-04-01T00:00:00.000Z";
    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(1);

    private IRepositoryFileRules Rules => f.Store.Synchronization.Files;

    // Task files are built through the fixture's literal seeding, then read back as bytes.
    private byte[] Task(string title = "T", string description = "", string rank = "a0", string[]? tags = null,
        string? resolved = null, string updated = T0, int version = 1)
    {
        f.Seed(id, title, rank, description, tags, resolved, version, updated: updated);
        return f.Files.ReadFile(f.TaskFile(id));
    }

    private JsonElement Merged(byte[]? basis, byte[]? local, byte[]? remote)
    {
        var merged = Rules.MergeTask(id, basis, local, remote);
        Assert.NotNull(merged);
        return JsonDocument.Parse(merged).RootElement;
    }

    // Paths

    [Fact]
    public void Names_the_managed_folders_and_settings_file()
    {
        Assert.Equal("tasks", Rules.TasksFolderName);
        Assert.Equal("images", Rules.ImagesFolderName);
        Assert.Equal("settings.json", Rules.SettingsFileName);
    }

    [Theory]
    [InlineData("tasks/01900000-0000-7000-8000-000000000001.json", RepositoryFileKind.Task)]
    [InlineData("images/01900000-0000-7000-8000-000000000001/pic.png", RepositoryFileKind.Image)]
    [InlineData("settings.json", RepositoryFileKind.Settings)]
    [InlineData("README.md", RepositoryFileKind.Unmanaged)]
    [InlineData("notes/settings.json", RepositoryFileKind.Unmanaged)]
    public void Classifies_a_repository_path(string path, RepositoryFileKind kind) => Assert.Equal(kind, Rules.Classify(path));

    [Fact]
    public void Finds_the_task_id_in_a_task_or_image_path_and_nothing_in_other_paths()
    {
        Assert.Equal(id, Rules.TaskIdInPath($"tasks/{id}.json"));
        Assert.Equal(id, Rules.TaskIdInPath($"images/{id}/pic.png"));
        Assert.Null(Rules.TaskIdInPath("tasks/not-an-id.json"));
        Assert.Null(Rules.TaskIdInPath("settings.json"));
    }

    [Theory]
    [InlineData("/tasks/x.json")]
    [InlineData("C:/tasks/x.json")]
    [InlineData("tasks/../settings.json")]
    [InlineData("./settings.json")]
    [InlineData("tasks/./x.json")]
    public void Refuses_a_rooted_path_or_one_with_dot_segments(string path)
    {
        Assert.Throws<ArgumentException>(() => Rules.Classify(path));
        Assert.Throws<ArgumentException>(() => Rules.TaskIdInPath(path));
    }

    // Task merge

    [Fact]
    public void Takes_the_change_of_the_one_device_that_changed_a_field()
    {
        var basis = Task("Old", "text", "a0");

        var merged = Merged(basis, Task("New", "text", "a0", updated: T1), Task("Old", "text", "a0"));

        Assert.Equal("New", merged.GetProperty("title").GetString());
        Assert.Equal("text", merged.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("rank")]
    public void Keeps_the_later_edit_when_both_devices_changed_a_field(string field)
    {
        var basis = Task("B", "B", "a5");
        byte[] Variant(string value, string updated) => field switch
        {
            "title" => Task(value, "B", "a5", updated: updated),
            "description" => Task("B", value, "a5", updated: updated),
            _ => Task("B", "B", value, updated: updated),
        };

        Assert.Equal("local", Merged(basis, Variant("local", T2), Variant("remote", T1)).GetProperty(field).GetString());
        Assert.Equal("remote", Merged(basis, Variant("local", T1), Variant("remote", T2)).GetProperty(field).GetString());
    }

    [Fact]
    public void Takes_the_remote_value_when_both_devices_edited_at_the_same_time()
    {
        var basis = Task("B");

        var merged = Merged(basis, Task("local", updated: T1), Task("remote", updated: T1));

        Assert.Equal("remote", merged.GetProperty("title").GetString());
    }

    [Fact]
    public void Applies_both_devices_tag_adds_and_removes()
    {
        var basis = Task(tags: ["a", "b", "c"]);

        var merged = Merged(basis, Task(tags: ["b", "c", "x"], updated: T1), Task(tags: ["a", "c", "y"], updated: T2));

        var tags = merged.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).Order().ToList();
        Assert.Equal(["c", "x", "y"], tags);
    }

    [Fact]
    public void Sets_updated_to_the_later_of_the_two_times()
    {
        var merged = Merged(Task(), Task("L", updated: T3), Task("R", updated: T1));

        Assert.Equal(T3, merged.GetProperty("updated").GetString());
    }

    [Fact]
    public void Keeps_a_task_resolved_when_the_other_device_edited_it()
    {
        var basis = Task("B");

        var merged = Merged(basis, Task("B", resolved: T1, updated: T1), Task("Edited", updated: T2));

        Assert.Equal(T1, merged.GetProperty("resolved").GetString());
        Assert.Equal("Edited", merged.GetProperty("title").GetString());
    }

    [Fact]
    public void Keeps_the_later_resolved_time_when_both_devices_changed_it()
    {
        var basis = Task("B");

        var merged = Merged(basis, Task("B", resolved: T1, updated: T1), Task("B", resolved: T2, updated: T2));

        Assert.Equal(T2, merged.GetProperty("resolved").GetString());
    }

    [Fact]
    public void Brings_back_an_edited_task_that_the_other_device_deleted()
    {
        var basis = Task("B");
        var edited = Task("Edited", updated: T1);

        Assert.Equal("Edited", Merged(basis, null, edited).GetProperty("title").GetString());
        Assert.Equal("Edited", Merged(basis, edited, null).GetProperty("title").GetString());
    }

    [Fact]
    public void Keeps_a_task_deleted_when_the_other_device_did_not_change_it_or_also_deleted_it()
    {
        var basis = Task("B");

        Assert.Null(Rules.MergeTask(id, basis, null, basis));
        Assert.Null(Rules.MergeTask(id, basis, basis, null));
        Assert.Null(Rules.MergeTask(id, basis, null, null));
    }

    [Fact]
    public void Creates_a_task_both_devices_added_without_a_basis_and_one_added_on_one_side()
    {
        var added = Task("New");

        Assert.Equal("New", Merged(null, added, null).GetProperty("title").GetString());
        Assert.Equal("New", Merged(null, null, added).GetProperty("title").GetString());
        Assert.Null(Rules.MergeTask(id, null, null, null));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("rank")]
    public void Keeps_the_later_edit_when_both_devices_added_the_task_without_a_basis(string field)
    {
        byte[] Variant(string value, string updated) => field switch
        {
            "title" => Task(value, "d", "a5", updated: updated),
            "description" => Task("t", value, "a5", updated: updated),
            _ => Task("t", "d", value, updated: updated),
        };

        Assert.Equal("local", Merged(null, Variant("local", T2), Variant("remote", T1)).GetProperty(field).GetString());
        Assert.Equal("remote", Merged(null, Variant("local", T1), Variant("remote", T2)).GetProperty(field).GetString());
    }

    [Fact]
    public void Takes_the_remote_value_and_the_later_resolved_time_when_both_devices_added_the_task_without_a_basis()
    {
        Assert.Equal("remote", Merged(null, Task("local", updated: T1), Task("remote", updated: T1)).GetProperty("title").GetString());
        Assert.Equal(T2, Merged(null, Task(resolved: T2, updated: T2), Task(resolved: T1, updated: T1)).GetProperty("resolved").GetString());
        Assert.Equal(T2, Merged(null, Task(resolved: T1, updated: T1), Task(resolved: T2, updated: T2)).GetProperty("resolved").GetString());
    }

    [Fact]
    public void Unites_the_tags_when_both_devices_added_the_task_without_a_basis()
    {
        var merged = Merged(null, Task(tags: ["a", "x"], updated: T1), Task(tags: ["a", "y"], updated: T2));

        var tags = merged.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).Order().ToList();
        Assert.Equal(["a", "x", "y"], tags);
    }

    [Fact]
    public void Merges_to_the_same_bytes_every_time_and_leaves_the_inputs_unchanged()
    {
        var basis = Task("B", tags: ["a"]);
        var local = Task("L", tags: ["a", "x"], updated: T1);
        var remote = Task("R", tags: [], updated: T2);
        var copies = new[] { basis.ToArray(), local.ToArray(), remote.ToArray() };

        var first = Rules.MergeTask(id, basis, local, remote);
        var second = Rules.MergeTask(id, basis, local, remote);

        Assert.Equal(first, second);
        Assert.Equal(copies[0], basis);
        Assert.Equal(copies[1], local);
        Assert.Equal(copies[2], remote);
        Assert.Contains("Z\"", Encoding.UTF8.GetString(first!));
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("local")]
    [InlineData("remote")]
    public void Refuses_to_merge_an_unreadable_or_newer_task_file(string which)
    {
        var good = Task();
        byte[] Bad() => Encoding.UTF8.GetBytes("{ not json");

        Assert.Throws<InvalidDataException>(() => Rules.MergeTask(id, which == "basis" ? Bad() : good, which == "local" ? Bad() : good, which == "remote" ? Bad() : good));
        Assert.Throws<InvalidDataException>(() => Rules.MergeTask(id, good, Task(version: 99), good));
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("local")]
    [InlineData("remote")]
    [InlineData("only local")]
    [InlineData("only remote")]
    [InlineData("basis of a deletion")]
    [InlineData("all three")]
    public void Refuses_to_merge_a_task_file_whose_task_id_differs_from_the_expected_one(string which)
    {
        var good = Task();
        var other = TasksFixture.Id(2);
        f.Seed(other, "Other");
        var wrong = f.Files.ReadFile(f.TaskFile(other));
        var copy = wrong.ToArray();

        Func<byte[]?> merge = which switch
        {
            "basis" => () => Rules.MergeTask(id, wrong, good, good),
            "local" => () => Rules.MergeTask(id, good, wrong, good),
            "remote" => () => Rules.MergeTask(id, good, good, wrong),
            "only local" => () => Rules.MergeTask(id, null, wrong, null),
            "only remote" => () => Rules.MergeTask(id, null, null, wrong),
            "all three" => () => Rules.MergeTask(id, wrong, wrong, wrong),
            _ => () => Rules.MergeTask(id, wrong, null, good),
        };

        Assert.Throws<InvalidDataException>(merge);
        Assert.Equal(copy, wrong);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("tasks/../x")]
    [InlineData("00000000-0000-0000-0000-00000000000A")]
    public void Rejects_an_expected_task_id_that_is_not_canonical(string? expected)
    {
        var good = Task();

        Assert.ThrowsAny<ArgumentException>(() => Rules.MergeTask(expected!, null, null, null));
        Assert.ThrowsAny<ArgumentException>(() => Rules.MergeTask(expected!, good, good, good));
    }

    // Settings merge

    private static byte[] Settings(int days = 7, string? add = null, string? manage = null,
        string daysTime = T0, string addTime = T0, string manageTime = T0, int version = 1) =>
        Encoding.UTF8.GetBytes(TasksFixture.SettingsJson(days, add, manage, version, daysTime, addTime, manageTime));

    private JsonElement MergedSettings(byte[]? basis, byte[]? local, byte[]? remote)
    {
        var merged = Rules.MergeSettings(basis, local, remote);
        Assert.NotNull(merged);
        return JsonDocument.Parse(merged).RootElement;
    }

    [Fact]
    public void Merges_each_setting_by_its_own_time()
    {
        var local = Settings(days: 30, add: "L", manage: "L", daysTime: T2, addTime: T1, manageTime: T3);
        var remote = Settings(days: 60, add: "R", manage: "R", daysTime: T1, addTime: T2, manageTime: T1);

        var merged = MergedSettings(Settings(), local, remote);

        Assert.Equal(30, merged.GetProperty("deletionDelayDays").GetProperty("value").GetInt32());
        Assert.Equal("R", merged.GetProperty("addNewTaskShortcut").GetProperty("value").GetString());
        Assert.Equal("L", merged.GetProperty("manageTasksShortcut").GetProperty("value").GetString());
        Assert.Equal(T2, merged.GetProperty("deletionDelayDays").GetProperty("updated").GetString());
    }

    [Fact]
    public void Takes_the_remote_setting_when_both_times_are_equal()
    {
        var merged = MergedSettings(null, Settings(days: 30, daysTime: T1), Settings(days: 60, daysTime: T1));

        Assert.Equal(60, merged.GetProperty("deletionDelayDays").GetProperty("value").GetInt32());
    }

    [Fact]
    public void Lets_a_present_file_win_over_an_absent_one_and_merges_three_absent_files_to_none()
    {
        var present = Settings(days: 30, add: "A", daysTime: T1);

        Assert.Equal(30, MergedSettings(null, present, null).GetProperty("deletionDelayDays").GetProperty("value").GetInt32());
        Assert.Equal("A", MergedSettings(Settings(), null, present).GetProperty("addNewTaskShortcut").GetProperty("value").GetString());
        Assert.Null(Rules.MergeSettings(null, null, null));
    }

    [Fact]
    public void Keeps_a_changed_setting_over_untouched_defaults()
    {
        var defaults = Settings(daysTime: "1970-01-01T00:00:00.000Z");
        var changed = Settings(days: 30, daysTime: T1);

        Assert.Equal(30, MergedSettings(defaults, defaults, changed).GetProperty("deletionDelayDays").GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "version": 1 }""")]
    [InlineData("""{ "version": 2, "deletionDelayDays": { "value": 7, "updated": "2026-01-01T00:00:00.000Z" } }""")]
    public void Refuses_to_merge_an_unreadable_or_newer_settings_file_and_leaves_the_inputs_unchanged(string bad)
    {
        var good = Settings();
        var badBytes = Encoding.UTF8.GetBytes(bad);
        var copy = badBytes.ToArray();

        Assert.Throws<InvalidDataException>(() => Rules.MergeSettings(good, badBytes, good));
        Assert.Throws<InvalidDataException>(() => Rules.MergeSettings(good, good, badBytes));
        Assert.Throws<InvalidDataException>(() => Rules.MergeSettings(badBytes, good, good));
        Assert.Equal(copy, badBytes);
    }
}
