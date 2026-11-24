using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>The tools that add, edit, move, resolve, reopen, and delete tasks, and add images: each one's result, its errors, and that it leaves the user's open work alone.</summary>
[Unit]
public class McpTaskChangeToolsTests
{
    private static string Png(int size = 0) => Convert.ToBase64String(FakeImageEncoder.Image(ImageFormat.Png, 100, 80, size));

    private static IEnumerable<string> ImageFiles(McpHarness app) =>
        app.Files.AllFilesUnder(McpHarness.Clone).Where(path => path.Contains("images", StringComparison.OrdinalIgnoreCase));

    // add_task

    [Fact]
    public async Task add_task_adds_a_task_at_the_top_and_answers_it()
    {
        await using var app = new McpHarness();
        app.AddTask("Older");

        var answer = await app.OkAsync("add_task", new { title = "Plan the trip", description = "Book **flights**.", tag = "Home" });

        var added = app.Tasks.All[0];
        Assert.Equal("Plan the trip", added.Title);
        Assert.Equal("Book **flights**.", added.Description);
        Assert.Equal(new[] { "Home" }, added.Tags);
        Assert.Equal(added.Id, answer.Str("id"));
        Assert.Equal("Plan the trip", answer.Str("title"));
        Assert.Equal("Book **flights**.", answer.Str("description"));
        Assert.Equal(new[] { "Home" }, answer.Strings("tags"));
    }

    [Fact]
    public async Task add_task_needs_only_a_title()
    {
        await using var app = new McpHarness();

        var answer = await app.OkAsync("add_task", new { title = "Just a title" });

        var added = Assert.Single(app.Tasks.All);
        Assert.Equal("Just a title", added.Title);
        Assert.Equal("", added.Description);
        Assert.Empty(added.Tags);
        Assert.Equal(added.Id, answer.Str("id"));
    }

    [Fact]
    public async Task add_task_spells_the_tag_as_a_matching_tag_in_use()
    {
        await using var app = new McpHarness();
        app.AddTask("Existing", "", "Work");

        await app.OkAsync("add_task", new { title = "New", tag = "work" });

        Assert.Equal(new[] { "Work" }, app.Tasks.All[0].Tags);
    }

    [Theory]
    [InlineData("")]
    [InlineData("first line\nsecond line")]
    public async Task add_task_refuses_an_empty_or_multi_line_title_and_adds_nothing(string title)
    {
        await using var app = new McpHarness();

        var error = await app.ErrorAsync("add_task", new { title });

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Empty(app.Tasks.All);
    }

    [Fact]
    public async Task add_task_leaves_the_draft_the_user_kept_as_it_is()
    {
        await using var app = new McpHarness();
        var draft = app.Tasks.StartDraft() with { Title = "My unfinished task", Description = "Half written" };
        app.Tasks.KeepDraft(draft);

        await app.OkAsync("add_task", new { title = "From the assistant", description = "Written by AI" });

        var kept = app.Tasks.LoadDraft();
        Assert.NotNull(kept);
        Assert.Equal(draft.TaskId, kept.TaskId);
        Assert.Equal("My unfinished task", kept.Title);
        Assert.Equal("Half written", kept.Description);
        Assert.Single(app.Tasks.All);
    }

    [Fact]
    public async Task add_task_fails_with_a_message_to_choose_a_repository_when_no_clone_is_selected()
    {
        await using var app = new McpHarness(cloneSelected: false);

        var error = await app.ErrorAsync("add_task", new { title = "Nowhere to go" });

        Assert.Contains("Configure", error);
    }

    [Fact]
    public async Task add_task_fails_with_the_message_of_a_file_that_cannot_be_written()
    {
        await using var app = new McpHarness();
        app.Files.FailWritesUnder(McpHarness.Clone);

        var error = await app.ErrorAsync("add_task", new { title = "Cannot save" });

        Assert.Contains("Writing is not allowed here", error);
        Assert.Empty(app.Tasks.All);
    }

    // edit_task

    [Fact]
    public async Task edit_task_changes_only_the_title_when_only_a_title_is_given()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Old title", "Keep this description", "Work");

        var answer = await app.OkAsync("edit_task", new { id = task.Id, title = "New title" });

        var saved = app.Tasks.All.Single();
        Assert.Equal("New title", saved.Title);
        Assert.Equal("Keep this description", saved.Description);
        Assert.Equal(new[] { "Work" }, saved.Tags);
        Assert.Equal("New title", answer.Str("title"));
        Assert.Equal("Keep this description", answer.Str("description"));
    }

    [Fact]
    public async Task edit_task_changes_only_the_description_when_only_a_description_is_given()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Keep this title", "Old description", "Work");

        await app.OkAsync("edit_task", new { id = task.Id, description = "New description" });

        var saved = app.Tasks.All.Single();
        Assert.Equal("Keep this title", saved.Title);
        Assert.Equal("New description", saved.Description);
        Assert.Equal(new[] { "Work" }, saved.Tags);
    }

    [Fact]
    public async Task edit_task_replaces_the_tag_when_a_tag_is_given()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Task", "Text", "Work");

        var answer = await app.OkAsync("edit_task", new { id = task.Id, tag = "Home" });

        Assert.Equal(new[] { "Home" }, app.Tasks.All.Single().Tags);
        Assert.Equal(new[] { "Home" }, answer.Strings("tags"));
        Assert.Equal("Task", app.Tasks.All.Single().Title);
    }

    [Fact]
    public async Task edit_task_removes_the_tag_when_the_tag_is_empty()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Task", "Text", "Work");

        await app.OkAsync("edit_task", new { id = task.Id, tag = "" });

        var saved = app.Tasks.All.Single();
        Assert.Empty(saved.Tags);
        Assert.Equal("Task", saved.Title);
        Assert.Equal("Text", saved.Description);
    }

    [Fact]
    public async Task edit_task_keeps_the_change_the_user_saved_earlier()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Old title", "Old description");
        app.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, "The user's description", []));

        await app.OkAsync("edit_task", new { id = task.Id, title = "Assistant's title" });

        var saved = app.Tasks.All.Single();
        Assert.Equal("Assistant's title", saved.Title);
        Assert.Equal("The user's description", saved.Description);
    }

    [Fact]
    public async Task edit_task_leaves_the_images_waiting_in_the_users_open_edit_unsaved()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Being edited", "Text");
        app.Tasks.SaveImage(task.Id, Convert.FromBase64String(Png()), "waiting");
        var waiting = app.Files.AllFilesUnder(McpHarness.DeviceFolder).Order().ToList();
        Assert.NotEmpty(waiting);

        await app.OkAsync("edit_task", new { id = task.Id, title = "Assistant's title" });

        Assert.Equal(waiting, app.Files.AllFilesUnder(McpHarness.DeviceFolder).Order());
        Assert.Empty(ImageFiles(app));
    }

    [Fact]
    public async Task edit_task_lets_a_later_save_of_the_users_open_edit_keep_the_assistants_title()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Old title", "Old description");

        await app.OkAsync("edit_task", new { id = task.Id, title = "Assistant's title" });
        app.Tasks.SaveEdit(task, new TaskDraft(task.Id, task.Title, "The user's description", []));

        var saved = app.Tasks.All.Single();
        Assert.Equal("Assistant's title", saved.Title);
        Assert.Equal("The user's description", saved.Description);
    }

    [Fact]
    public async Task edit_task_spells_the_tag_as_a_matching_tag_in_use()
    {
        await using var app = new McpHarness();
        app.AddTask("Existing", "", "Work");
        var task = app.AddTask("Other", "", "Home");

        var answer = await app.OkAsync("edit_task", new { id = task.Id, tag = "work" });

        Assert.Equal(new[] { "Work" }, app.Tasks.All.Single(t => t.Id == task.Id).Tags);
        Assert.Equal(new[] { "Work" }, answer.Strings("tags"));
    }

    [Fact]
    public async Task edit_task_removes_every_tag_when_the_tag_is_empty()
    {
        await using var app = new McpHarness();
        var task = app.SeedTask("Many tags", "Work", "Home", "Errand");

        await app.OkAsync("edit_task", new { id = task.Id, tag = "" });

        Assert.Empty(app.Tasks.All.Single().Tags);
    }

    [Fact]
    public async Task edit_task_keeps_every_tag_when_no_tag_is_given()
    {
        await using var app = new McpHarness();
        var task = app.SeedTask("Many tags", "Work", "Home", "Errand");

        var answer = await app.OkAsync("edit_task", new { id = task.Id, title = "Renamed" });

        Assert.Equal(new[] { "Work", "Home", "Errand" }, app.Tasks.All.Single().Tags);
        Assert.Equal(new[] { "Work", "Home", "Errand" }, answer.Strings("tags"));
        Assert.Equal("Renamed", app.Tasks.All.Single().Title);
    }

    [Fact]
    public async Task edit_task_fails_with_a_changed_meanwhile_error_and_writes_nothing_when_the_task_changed_after_it_was_read()
    {
        TaskItem? task = null;
        await using var app = new McpHarness(wrapDirectForServer: direct => new DirectBeforeUpdate(direct, () =>
            _ = direct.Update(task!.Id, task.Updated, null, "The user's description", null)));
        task = app.AddTask("Old title", "Old description");

        var error = await app.ErrorAsync("edit_task", new { id = task.Id, title = "Assistant's title" });

        Assert.Contains("changed meanwhile", error, StringComparison.OrdinalIgnoreCase);
        var saved = app.Tasks.All.Single();
        Assert.Equal("Old title", saved.Title);
        Assert.Equal("The user's description", saved.Description);
    }

    [Fact]
    public async Task edit_task_does_not_retry_a_stale_edit_by_itself()
    {
        var updates = 0;
        TaskItem? task = null;
        await using var app = new McpHarness(wrapDirectForServer: direct => new DirectBeforeUpdate(direct, () =>
        {
            updates++;
            if (updates == 1)
            {
                direct.Update(task!.Id, task.Updated, null, "The user's description", null);
            }
        }));
        task = app.AddTask("Old title", "Old description");

        await app.ErrorAsync("edit_task", new { id = task.Id, title = "Assistant's title" });

        Assert.Equal(1, updates);
        Assert.Equal("Old title", app.Tasks.All.Single().Title);
    }

    [Fact]
    public async Task edit_task_fails_for_an_unknown_id_naming_it()
    {
        await using var app = new McpHarness();
        app.AddTask("Something");

        var error = await app.ErrorAsync("edit_task", new { id = "no-such-task", title = "New" });

        Assert.Contains("no-such-task", error);
    }

    [Fact]
    public async Task edit_task_refuses_an_empty_title_and_changes_nothing()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Keep me", "Text");

        await app.ErrorAsync("edit_task", new { id = task.Id, title = "" });

        Assert.Equal("Keep me", app.Tasks.All.Single().Title);
    }

    [Fact]
    public async Task edit_task_fails_with_a_message_to_choose_a_repository_when_no_clone_is_selected()
    {
        await using var app = new McpHarness(cloneSelected: false);

        var error = await app.ErrorAsync("edit_task", new { id = "any", title = "New" });

        Assert.Contains("Configure", error);
    }

    // add_image

    [Fact]
    public async Task add_image_appends_the_images_markdown_on_a_new_line_and_answers_the_task_as_saved()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("With a picture", "Text");

        var answer = await app.OkAsync("add_image", new { id = task.Id, image = Png(), altText = "a photo" });

        var saved = app.Tasks.All.Single();
        var lines = saved.Description.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Text", lines[0]);
        Assert.StartsWith($"![a photo](images/{task.Id}/", lines[1]);
        Assert.EndsWith(")", lines[1]);
        Assert.Single(ImageFiles(app));
        Assert.Equal(saved.Id, answer.Str("id"));
        Assert.Equal("With a picture", answer.Str("title"));
        Assert.Equal(saved.Description, answer.Str("description"));
        Assert.Equal(saved.Updated, answer.GetProperty("updated").GetDateTimeOffset());
    }

    [Fact]
    public async Task add_image_leaves_the_kept_draft_and_the_images_waiting_in_an_open_edit_as_they_are()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("With a picture", "Text");
        app.Tasks.SaveImage(task.Id, Convert.FromBase64String(Png()), "waiting");
        var draft = app.Tasks.StartDraft() with { Title = "Kept draft" };
        app.Tasks.KeepDraft(draft);
        var device = app.Files.AllFilesUnder(McpHarness.DeviceFolder).Order().ToList();

        await app.OkAsync("add_image", new { id = task.Id, image = Png(), altText = "a photo" });

        Assert.Equal(device, app.Files.AllFilesUnder(McpHarness.DeviceFolder).Order());
        Assert.Equal("Kept draft", app.Tasks.LoadDraft()?.Title);
    }

    [Fact]
    public async Task add_image_refuses_bytes_over_20_MB_before_Tasks_sees_them()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Too big");

        var error = await app.ErrorAsync("add_image", new { id = task.Id, image = Png(size: 20 * 1024 * 1024 + 1), altText = "huge" });

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Empty(app.Images.Calls);
        Assert.Empty(ImageFiles(app));
    }

    [Fact]
    public async Task add_image_refuses_bytes_that_are_not_an_image()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Not a picture");

        await app.ErrorAsync("add_image", new { id = task.Id, image = Convert.ToBase64String("plain text"u8.ToArray()), altText = "text" });

        Assert.Empty(ImageFiles(app));
    }

    [Fact]
    public async Task add_image_refuses_bytes_that_are_not_valid_base64()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Bad bytes");

        var error = await app.ErrorAsync("add_image", new { id = task.Id, image = "***not base64***", altText = "text" });

        Assert.Contains("base64", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(ImageFiles(app));
    }

    [Fact]
    public async Task add_image_fails_for_an_unknown_task_naming_it()
    {
        await using var app = new McpHarness();

        var error = await app.ErrorAsync("add_image", new { id = "no-such-task", image = Png(), altText = "text" });

        Assert.Contains("no-such-task", error);
        Assert.Empty(ImageFiles(app));
    }

    // move_task

    [Fact]
    public async Task move_task_puts_the_task_between_the_two_given_tasks()
    {
        await using var app = new McpHarness();
        var a = app.AddTask("A");
        var b = app.AddTask("B");
        var c = app.AddTask("C");
        Assert.Equal(new[] { c.Id, b.Id, a.Id }, app.Tasks.All.Select(task => task.Id));

        var answer = await app.OkAsync("move_task", new { id = a.Id, previousId = c.Id, nextId = b.Id });

        Assert.Equal(new[] { c.Id, a.Id, b.Id }, app.Tasks.All.Select(task => task.Id));
        Assert.Equal(a.Id, answer.Str("id"));
    }

    [Fact]
    public async Task move_task_puts_the_task_at_the_top_when_no_previous_task_is_given()
    {
        await using var app = new McpHarness();
        var a = app.AddTask("A");
        app.AddTask("B");
        var c = app.AddTask("C");

        await app.OkAsync("move_task", new { id = a.Id, nextId = c.Id });

        Assert.Equal(a.Id, app.Tasks.All[0].Id);
    }

    [Fact]
    public async Task move_task_fails_for_an_unknown_neighbor_and_changes_nothing()
    {
        await using var app = new McpHarness();
        var a = app.AddTask("A");
        var b = app.AddTask("B");

        var error = await app.ErrorAsync("move_task", new { id = a.Id, nextId = "no-such-task" });

        Assert.Contains("no-such-task", error);
        Assert.Equal(new[] { b.Id, a.Id }, app.Tasks.All.Select(task => task.Id));
    }

    // resolve_task, reopen_task, delete_task

    [Fact]
    public async Task resolve_task_resolves_the_task_and_reopen_task_reopens_it()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Finish me");

        var resolved = await app.OkAsync("resolve_task", new { id = task.Id });
        var whileResolved = app.Tasks.All.Single().Resolved;
        var reopened = await app.OkAsync("reopen_task", new { id = task.Id });

        Assert.NotNull(whileResolved);
        Assert.False(resolved.IsAbsent("resolved"));
        Assert.Null(app.Tasks.All.Single().Resolved);
        Assert.True(reopened.IsAbsent("resolved"));
    }

    [Theory]
    [InlineData("resolve_task")]
    [InlineData("reopen_task")]
    [InlineData("delete_task")]
    public async Task Resolve_reopen_and_delete_fail_for_an_unknown_id_naming_it(string tool)
    {
        await using var app = new McpHarness();
        app.AddTask("Something");

        var error = await app.ErrorAsync(tool, new { id = "no-such-task" });

        Assert.Contains("no-such-task", error);
        Assert.Single(app.Tasks.All);
    }

    [Fact]
    public async Task delete_task_deletes_the_task_at_once()
    {
        await using var app = new McpHarness();
        var doomed = app.AddTask("Delete me");
        var kept = app.AddTask("Keep me");

        var answer = await app.OkAsync("delete_task", new { id = doomed.Id });

        Assert.Equal(new[] { kept.Id }, app.Tasks.All.Select(task => task.Id));
        Assert.Equal(doomed.Id, answer.Str("deleted"));
    }

    [Theory]
    [InlineData("resolve_task")]
    [InlineData("reopen_task")]
    [InlineData("delete_task")]
    [InlineData("move_task")]
    public async Task Every_task_change_fails_with_a_message_to_choose_a_repository_when_no_clone_is_selected(string tool)
    {
        await using var app = new McpHarness(cloneSelected: false);

        var error = await app.ErrorAsync(tool, new { id = "any" });

        Assert.Contains("Configure", error);
    }
}
