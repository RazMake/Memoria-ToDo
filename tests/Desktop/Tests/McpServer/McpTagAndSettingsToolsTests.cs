using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.McpServer.Tests;

/// <summary>The tools for tag colors and order, the deletion delay, start with Windows, sync, and updates.</summary>
[Unit]
public class McpTagAndSettingsToolsTests
{
    // tags

    [Fact]
    public async Task set_tag_color_sets_the_color_of_the_tag()
    {
        await using var app = new McpHarness();

        await app.OkAsync("set_tag_color", new { tag = "Work", color = "red" });

        Assert.Contains(("Work", "red"), app.TagColors.Kept);
    }

    [Fact]
    public async Task set_tag_color_fails_with_the_modules_message_for_an_unknown_color()
    {
        await using var app = new McpHarness();
        app.TagColors.Failure = new ArgumentException("No such color: chartreuse.");

        var error = await app.ErrorAsync("set_tag_color", new { tag = "Work", color = "chartreuse" });

        Assert.Contains("No such color: chartreuse.", error);
    }

    [Fact]
    public async Task move_tag_puts_the_tag_between_the_two_given_tags_and_answers_the_new_order()
    {
        await using var app = new McpHarness();
        app.AddTask("A", "", "Alpha");
        app.AddTask("B", "", "Beta");
        app.AddTask("C", "", "Gamma");
        app.TagOrder.Seed("Alpha", "Beta", "Gamma");

        var answer = await app.OkAsync("move_tag", new { tag = "Gamma", previousTag = "Alpha", nextTag = "Beta" });

        Assert.Contains(("Gamma", (string?)"Alpha", (string?)"Beta"), app.TagOrder.Moves);
        Assert.Equal(new[] { "Alpha", "Gamma", "Beta" }, answer.Items("tags").Select(tag => tag.Str("tag")));
    }

    // deletion delay

    [Fact]
    public async Task get_deletion_delay_answers_the_days_a_resolved_task_stays()
    {
        await using var app = new McpHarness();
        app.Settings.SetDeletionDelayDays(14);

        var answer = await app.OkAsync("get_deletion_delay");

        Assert.Equal(14, answer.GetProperty("days").GetInt32());
    }

    [Fact]
    public async Task set_deletion_delay_saves_the_days_and_answers_them()
    {
        await using var app = new McpHarness();

        var answer = await app.OkAsync("set_deletion_delay", new { days = 30 });

        Assert.Equal(new[] { 30 }, app.Settings.SavedDelays);
        Assert.Equal(30, answer.GetProperty("days").GetInt32());
    }

    [Fact]
    public async Task set_deletion_delay_refuses_days_outside_the_range_the_module_accepts_and_saves_nothing()
    {
        await using var app = new McpHarness();

        await app.ErrorAsync("set_deletion_delay", new { days = 0 });
        await app.ErrorAsync("set_deletion_delay", new { days = 366 });

        Assert.Empty(app.Settings.SavedDelays);
    }

    // start with Windows

    [WpfFact]
    public async Task get_start_with_windows_answers_whether_it_is_on()
    {
        await using var app = new McpHarness();
        app.StartOff();
        app.StartWithWindows.On = true;

        var answer = await app.OkAsync("get_start_with_windows");

        Assert.True(answer.GetProperty("on").GetBoolean());
    }

    [WpfFact]
    public async Task set_start_with_windows_turns_it_on_and_off_and_answers_the_new_state()
    {
        await using var app = new McpHarness();
        app.StartOff();

        var on = await app.OkAsync("set_start_with_windows", new { on = true });
        var off = await app.OkAsync("set_start_with_windows", new { on = false });

        Assert.True(on.GetProperty("on").GetBoolean());
        Assert.False(off.GetProperty("on").GetBoolean());
        Assert.Equal(new[] { true, false }, app.StartWithWindows.SetCalls);
    }

    [WpfFact]
    public async Task set_start_with_windows_runs_on_the_UI_thread()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var uiThread = Environment.CurrentManagedThreadId;

        await app.OkAsync("set_start_with_windows", new { on = true });

        Assert.Contains(uiThread, app.StartWithWindows.CallThreads);
        Assert.All(app.StartWithWindows.CallThreads, thread => Assert.Equal(uiThread, thread));
    }

    // sync

    [Fact]
    public async Task get_sync_status_answers_the_state_and_what_the_user_sees_in_Configure()
    {
        await using var app = new McpHarness();

        var answer = await app.OkAsync("get_sync_status");

        Assert.False(answer.IsAbsent("state"));
        Assert.True(answer.TryGetProperty("hasPendingChanges", out _));
        Assert.True(answer.TryGetProperty("deviceName", out _));
    }

    [Fact]
    public async Task sync_now_asks_for_a_sync_once()
    {
        await using var app = new McpHarness();

        await app.OkAsync("sync_now");

        Assert.Equal(1, app.Sync.SyncCount);
    }

    [Fact]
    public async Task set_device_name_saves_the_name_and_answers_it()
    {
        await using var app = new McpHarness();

        var answer = await app.OkAsync("set_device_name", new { name = "Work laptop" });

        Assert.Equal("Work laptop", app.Sync.SetupControl.Snapshot.DeviceName);
        Assert.Equal("Work laptop", answer.Str("deviceName"));
    }

    [Fact]
    public async Task set_device_name_fails_with_the_modules_message_for_a_name_it_refuses()
    {
        await using var app = new McpHarness();
        app.Sync.SetupControl.Failure = new ArgumentException("The name must not be empty.");

        var error = await app.ErrorAsync("set_device_name", new { name = "" });

        Assert.Contains("The name must not be empty.", error);
    }

    // updates

    [Fact]
    public async Task get_update_status_answers_the_stage_and_versions()
    {
        await using var app = new McpHarness();

        var answer = await app.OkAsync("get_update_status");

        Assert.False(answer.IsAbsent("stage"));
        Assert.True(answer.TryGetProperty("autoUpdate", out _));
        Assert.True(answer.TryGetProperty("currentVersion", out _));
    }

    [WpfFact]
    public async Task set_auto_update_changes_the_setting_and_answers_the_new_state()
    {
        await using var app = new McpHarness();
        app.StartOff();

        var answer = await app.OkAsync("set_auto_update", new { on = false });

        Assert.Equal(new[] { false }, app.Updates.SetAutoUpdateCalls);
        Assert.False(answer.GetProperty("autoUpdate").GetBoolean());
    }

    [WpfFact]
    public async Task set_auto_update_runs_on_the_UI_thread()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var uiThread = Environment.CurrentManagedThreadId;
        var threads = new List<int>();
        app.Updates.Changed += (_, _) => { lock (threads) { threads.Add(Environment.CurrentManagedThreadId); } };

        await app.OkAsync("set_auto_update", new { on = !app.Updates.AutoUpdate });

        Assert.NotEmpty(threads);
        Assert.All(threads, thread => Assert.Equal(uiThread, thread));
    }
}
