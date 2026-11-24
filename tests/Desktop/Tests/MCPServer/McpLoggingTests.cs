using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>What the server logs for its tool calls, by the logging design: Information for each call, Warning for refused input, Error for a failed operation, and never a task's description or a contact's fields.</summary>
[Unit]
public class McpLoggingTests
{
    [Fact]
    public async Task A_read_tool_call_logs_at_Information_naming_the_tool()
    {
        await using var app = new McpHarness();

        await app.OkAsync("list_tasks");

        app.Log.Events.SingleAt(LogEventLevel.Information, "list_tasks");
        app.Log.Events.AssertNoProblems();
    }

    [Fact]
    public async Task A_change_call_logs_at_Information_naming_the_tool_and_the_task_id()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Resolve me");
        var before = app.Log.Events.Count;

        await app.OkAsync("resolve_task", new { id = task.Id });

        app.Log.After(before).SingleAt(LogEventLevel.Information, "resolve_task", task.Id);
        app.Log.After(before).AssertNoProblems();
    }

    [Fact]
    public async Task add_task_logs_the_id_of_the_task_it_added()
    {
        await using var app = new McpHarness();

        var answer = await app.OkAsync("add_task", new { title = "New task" });

        app.Log.Events.SingleAt(LogEventLevel.Information, "add_task", answer.Str("id")!);
    }

    [Fact]
    public async Task Refused_input_logs_a_Warning_naming_the_tool_and_the_reason()
    {
        await using var app = new McpHarness();

        var error = await app.ErrorAsync("get_task", new { id = "no-such-task" });

        var entry = app.Log.Events.SingleAt(LogEventLevel.Warning, "get_task");
        Assert.Contains("no-such-task", entry.EverythingIn());
        Assert.Contains("no-such-task", error);
        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [Fact]
    public async Task A_stale_edit_logs_a_Warning_with_only_the_exception_type_and_no_Error()
    {
        TaskItem? task = null;
        await using var app = new McpHarness(wrapDirectForServer: direct => new DirectBeforeUpdate(direct, () =>
            _ = direct.Update(task!.Id, task.Updated, null, "Secret-Description", null)));
        task = app.AddTask("Old title", "Old description");
        var before = app.Log.Events.Count;

        await app.ErrorAsync("edit_task", new { id = task.Id, title = "Assistant's title" });

        var entry = app.Log.After(before).SingleAt(LogEventLevel.Warning, "edit_task", task.Id, "StaleTaskException");
        Assert.DoesNotContain("Secret-Description", entry.EverythingIn(), StringComparison.Ordinal);
        Assert.Empty(app.Log.After(before).At(LogEventLevel.Error));
    }

    [Fact]
    public async Task A_call_with_no_clone_selected_logs_a_Warning()
    {
        await using var app = new McpHarness(cloneSelected: false);

        await app.ErrorAsync("list_tasks");

        app.Log.Events.SingleAt(LogEventLevel.Warning, "list_tasks");
        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [Fact]
    public async Task A_failed_operation_logs_an_Error_with_the_exception()
    {
        await using var app = new McpHarness();
        app.Files.FailWritesUnder(McpHarness.Clone);

        await app.ErrorAsync("add_task", new { title = "Cannot save" });

        var entry = app.Log.Events.SingleAt(LogEventLevel.Error, "add_task");
        Assert.Contains("Writing is not allowed here", entry.EverythingIn());
    }

    [Fact]
    public async Task A_log_that_cannot_be_read_logs_an_Error_for_read_log()
    {
        await using var app = new McpHarness();
        app.Logs.Failure = new IOException("The log folder is gone.");

        await app.ErrorAsync("read_log");

        var entry = app.Log.Events.SingleAt(LogEventLevel.Error, "read_log");
        Assert.Contains("The log folder is gone.", entry.EverythingIn());
    }

    [Fact]
    public async Task Never_logs_a_task_description()
    {
        await using var app = new McpHarness();
        const string Secret = "SECRET-DESCRIPTION-TEXT";
        var task = app.AddTask("Title", "old");

        await app.OkAsync("add_task", new { title = "New", description = Secret });
        await app.OkAsync("edit_task", new { id = task.Id, description = Secret });
        await app.OkAsync("get_task", new { id = task.Id });
        app.Files.FailWritesUnder(McpHarness.Clone);
        await app.ErrorAsync("edit_task", new { id = task.Id, description = Secret + " changed" });

        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Never_logs_a_contacts_fields()
    {
        await using var app = new McpHarness();
        app.UseContacts(ContactFilesPerson());
        const string Secret = "SECRET-TITLE-TEXT";

        await app.OkAsync("save_contact", new { group = "team", id = "5", fullName = "Eve Secret-Name", alias = "eve", title = Secret });
        await app.OkAsync("find_contact", new { name = "eve" });
        await app.ErrorAsync("save_contact", new { group = "no-such-group", id = "6", fullName = "Frank Secret-Name", title = Secret });

        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains(Secret, StringComparison.Ordinal));
        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains("Secret-Name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Never_logs_the_image_bytes_or_the_alt_text()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Picture");
        const string Alt = "SECRET-ALT-TEXT";
        var image = Convert.ToBase64String(Shared.Fakes.FakeImageEncoder.Image(Shared.Tasks.ImageFormat.Png, 10, 10));

        await app.OkAsync("add_image", new { id = task.Id, image, altText = Alt });

        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains(Alt, StringComparison.Ordinal));
        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains(image, StringComparison.Ordinal));
    }

    private static string ContactFilesPerson() => Shared.Fakes.ContactFiles.Person("1", "Ana Pop", ("alias", "ana"));

    [Fact]
    public async Task A_contact_refused_for_an_alias_another_contact_holds_logs_a_Warning_without_the_alias_while_the_assistant_gets_the_message()
    {
        await using var app = new McpHarness();
        const string Alias = "held-alias-4711";
        app.UseContacts(Shared.Fakes.ContactFiles.Person("1", "Ana Pop", ("alias", Alias)));

        var error = await app.ErrorAsync("save_contact", new { group = "team", id = "2", fullName = "Bob Ionescu", alias = Alias });

        Assert.Contains(Alias, error);
        app.Log.Events.SingleAt(LogEventLevel.Warning, "save_contact");
        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains(Alias, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_contact_refused_for_an_invalid_date_logs_a_Warning_without_the_input_while_the_assistant_gets_the_message()
    {
        await using var app = new McpHarness();
        const string BadDate = "not-a-date-4711";
        app.UseContacts(ContactFilesPerson());

        var error = await app.ErrorAsync("save_contact", new { group = "team", id = "1", fullName = "Ana Pop", levelStartDate = BadDate });

        Assert.Contains("yyyy-MM-dd", error);
        app.Log.Events.SingleAt(LogEventLevel.Warning, "save_contact");
        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains(BadDate, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_limit_that_is_not_a_whole_number_logs_a_Warning_naming_read_log()
    {
        await using var app = new McpHarness();

        Assert.True(await app.WasRefusedAsync("read_log", new { limit = 2.5 }));

        app.Log.Events.SingleAt(LogEventLevel.Warning, "read_log");
        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [Fact]
    public async Task A_limit_given_as_text_logs_a_Warning_naming_read_log()
    {
        await using var app = new McpHarness();

        Assert.True(await app.WasRefusedAsync("read_log", new { limit = "many" }));

        app.Log.Events.SingleAt(LogEventLevel.Warning, "read_log");
        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [Fact]
    public async Task A_missing_required_input_logs_a_Warning_naming_the_tool()
    {
        await using var app = new McpHarness();

        Assert.True(await app.WasRefusedAsync("get_task", null));

        app.Log.Events.SingleAt(LogEventLevel.Warning, "get_task");
        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [Fact]
    public async Task read_log_never_logs_the_text_it_searches_for()
    {
        await using var app = new McpHarness();
        const string Searched = "SECRET-SEARCH-TEXT";

        await app.OkAsync("read_log", new { text = Searched });
        await app.ErrorAsync("read_log", new { text = Searched, limit = 0 });
        app.Logs.Failure = new IOException("The log folder is gone.");
        await app.ErrorAsync("read_log", new { text = Searched });

        Assert.DoesNotContain(app.Log.Events, entry => entry.EverythingIn().Contains(Searched, StringComparison.Ordinal));
    }

    [WpfFact]
    public void Starting_with_the_switch_off_logs_it_at_Information()
    {
        var app = new McpHarness();

        app.StartOff();

        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Information));
        app.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task Stopping_logs_it_at_Information()
    {
        var app = new McpHarness();
        app.StartOff();
        var before = app.Log.Events.Count;

        await app.Server.StopAsync().WaitAsync(McpHarness.Bound);

        Assert.NotEmpty(app.Log.After(before).At(LogEventLevel.Information));
        app.Log.Events.AssertNoProblems();
    }
}
