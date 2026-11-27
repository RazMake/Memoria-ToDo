using System.IO;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using ModelContextProtocol.Protocol;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>
/// A change Tasks or Contacts saved before a notice failed is a success for the assistant: the tool returns what it returns after
/// a save, with one sentence saying the app couldn't finish updating, and logs one Error entry. So an assistant that repeats a call
/// only after an error never adds a second task for one <c>add_task</c>.
/// </summary>
[Unit]
public class McpNoticeFailureTests
{
    private const string Sentence = "The change was saved, but the app couldn't finish updating after it.";

    private static readonly NoticeFailedException<TaskItem> TaskNoticeFailure = new(null, [new IOException("Changed failed.")]);

    private static McpHarness WithFailingNotices(out TestTaskStore.NoticeFailures notices)
    {
        var app = new McpHarness();
        notices = app.Tasks.FailNotices();
        notices.Active = true;
        return app;
    }

    private static async Task<CallToolResult> SavedAsync(McpHarness app, string tool, object arguments)
    {
        var result = await app.CallAsync(tool, arguments);

        Assert.True(result.IsError != true, $"{tool} should have succeeded but answered: {result.Text()}");
        Assert.Contains(Sentence, result.Text());
        var entry = app.Log.Events.SingleAt(LogEventLevel.Error, tool);
        Assert.NotNull(entry.Exception);
        return result;
    }

    [Fact]
    public async Task add_task_succeeds_with_the_new_task_and_its_id_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = WithFailingNotices(out _);

        var result = await SavedAsync(app, "add_task", new { title = "Plan the trip" });

        var added = Assert.Single(app.Tasks.All);
        Assert.Contains(added.Id, result.Text());
        Assert.Contains("Plan the trip", result.Text());
    }

    [Fact]
    public async Task add_task_after_this_failure_is_never_an_error_so_a_client_never_adds_the_task_twice()
    {
        await using var app = WithFailingNotices(out _);

        var result = await app.CallAsync("add_task", new { title = "Only once" });

        Assert.True(result.IsError != true);
        Assert.Single(app.Tasks.All);
    }

    [Fact]
    public async Task edit_task_succeeds_with_the_task_as_saved_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Old title");
        app.Tasks.FailNotices().Active = true;

        var result = await SavedAsync(app, "edit_task", new { id = task.Id, title = "New title" });

        Assert.Contains("New title", result.Text());
        Assert.Equal("New title", app.Tasks.All.Single().Title);
    }

    [Fact]
    public async Task add_image_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("With a photo");
        app.Tasks.FailNotices().Active = true;
        var image = Convert.ToBase64String(FakeImageEncoder.Image(ImageFormat.Png, 100, 80, 0));

        await SavedAsync(app, "add_image", new { id = task.Id, image, altText = "a photo" });
    }

    [Fact]
    public async Task move_task_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        var b = app.AddTask("B");
        var a = app.AddTask("A");
        app.Tasks.FailNotices().Active = true;

        await SavedAsync(app, "move_task", new { id = a.Id, previousId = b.Id });
    }

    [Fact]
    public async Task resolve_task_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Buy milk");
        app.Tasks.FailNotices().Active = true;

        await SavedAsync(app, "resolve_task", new { id = task.Id });

        Assert.NotNull(app.Tasks.All.Single().Resolved);
    }

    [Fact]
    public async Task reopen_task_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Buy milk");
        await app.OkAsync("resolve_task", new { id = task.Id });
        app.Tasks.FailNotices().Active = true;

        await SavedAsync(app, "reopen_task", new { id = task.Id });

        Assert.Null(app.Tasks.All.Single().Resolved);
    }

    [Fact]
    public async Task delete_task_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Doomed");
        app.Tasks.FailNotices().Active = true;

        await SavedAsync(app, "delete_task", new { id = task.Id });

        Assert.Empty(app.Tasks.All);
    }

    [Fact]
    public async Task set_tag_color_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        app.TagColors.Failure = TaskNoticeFailure;

        await SavedAsync(app, "set_tag_color", new { tag = "Work", color = "red" });
    }

    [Fact]
    public async Task move_tag_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        app.TagOrder.Seed("alpha", "beta", "gamma");
        app.TagOrder.SavesBeforeFailing = true;
        app.TagOrder.Failure = TaskNoticeFailure;

        await SavedAsync(app, "move_tag", new { tag = "gamma", previousTag = "alpha", nextTag = "beta" });
    }

    [Fact]
    public async Task set_deletion_delay_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = new McpHarness();
        app.Settings.SetFailure = TaskNoticeFailure;

        await SavedAsync(app, "set_deletion_delay", new { days = 30 });
    }

    private static McpHarness WithTeamAndFailingContactNotice(Action<McpHarness>? seedMore = null)
    {
        var app = new McpHarness();
        app.UseContacts(
            ContactFiles.Person("1", "Ana Pop", ("alias", "ana")),
            ContactFiles.Person("2", "Bob Ionescu", ("alias", "bob")));
        app.Files.SeedDataTypes(McpHarness.Clone);
        seedMore?.Invoke(app);
        app.Contacts.UseRepositoryFolder(McpHarness.Clone);
        app.Contacts.Changed += (_, _) => throw new IOException("Changed failed.");
        return app;
    }

    [Fact]
    public async Task save_contact_succeeds_with_the_saved_contact_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = WithTeamAndFailingContactNotice();

        var result = await SavedAsync(app, "save_contact", new { group = "team", id = "3", fullName = "Cara Dan", alias = "cara" });

        Assert.Contains("Cara Dan", result.Text());
        Assert.Equal("Cara Dan", app.Contacts.Find("cara")?.FullName);
    }

    [Fact]
    public async Task delete_contact_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = WithTeamAndFailingContactNotice();

        await SavedAsync(app, "delete_contact", new { group = "team", id = "2" });

        Assert.Null(app.Contacts.Find("bob"));
    }

    [Fact]
    public async Task move_contact_group_succeeds_and_says_the_app_couldnt_finish_updating()
    {
        await using var app = WithTeamAndFailingContactNotice(seeded =>
        {
            seeded.Files.SeedGroup(McpHarness.Clone, "alpha", 100, ContactFiles.Person("10", "Alpha One"));
            seeded.Files.SeedGroup(McpHarness.Clone, "beta", 200, ContactFiles.Person("11", "Beta One"));
        });

        await SavedAsync(app, "move_contact_group", new { group = "beta", nextGroup = "alpha" });
    }
}
