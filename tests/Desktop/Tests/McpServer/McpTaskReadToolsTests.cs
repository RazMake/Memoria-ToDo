using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.McpServer.Tests;

/// <summary>The tools that read tasks and tags: what they answer, and the errors they give.</summary>
[Unit]
public class McpTaskReadToolsTests
{
    [Fact]
    public async Task list_tasks_answers_every_open_and_resolved_task_in_rank_order_without_descriptions()
    {
        await using var app = new McpHarness();
        var first = app.AddTask("First", "First description");
        var second = app.AddTask("Second", "Second description", "Work");
        app.Tasks.Resolve(first.Id);

        var answer = await app.OkAsync("list_tasks");

        var tasks = answer.Items("tasks");
        Assert.Equal(app.Tasks.All.Select(task => task.Id), tasks.Select(task => task.Str("id")));
        Assert.Equal([second.Id, first.Id], tasks.Select(task => task.Str("id")));
        Assert.All(tasks, task => Assert.True(task.IsAbsent("description")));
        Assert.Equal(["Work"], tasks[0].Strings("tags"));
        Assert.True(tasks[0].IsAbsent("resolved"));
        Assert.False(tasks[1].IsAbsent("resolved"));
    }

    [Fact]
    public async Task list_tasks_answers_an_empty_list_when_the_clone_has_no_tasks()
    {
        await using var app = new McpHarness();

        var answer = await app.OkAsync("list_tasks");

        Assert.Empty(answer.Items("tasks"));
    }

    [Fact]
    public async Task get_task_answers_one_task_with_its_description()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Write the report", "Cover **all** the numbers.", "Work");

        var answer = await app.OkAsync("get_task", new { id = task.Id });

        Assert.Equal(task.Id, answer.Str("id"));
        Assert.Equal("Write the report", answer.Str("title"));
        Assert.Equal("Cover **all** the numbers.", answer.Str("description"));
        Assert.Equal(["Work"], answer.Strings("tags"));
    }

    [Fact]
    public async Task get_task_fails_with_a_not_found_error_naming_an_unknown_id()
    {
        await using var app = new McpHarness();
        app.AddTask("Something");

        var error = await app.ErrorAsync("get_task", new { id = "no-such-task" });

        Assert.Contains("no-such-task", error);
    }

    [Fact]
    public async Task search_tasks_answers_the_tasks_that_hold_every_word_in_rank_order()
    {
        await using var app = new McpHarness();
        var other = app.AddTask("Buy milk", "From the shop");
        var older = app.AddTask("Call the dentist", "About the red filling");
        var newer = app.AddTask("Red car wash");

        var answer = await app.OkAsync("search_tasks", new { text = "red" });

        Assert.Equal([newer.Id, older.Id], answer.Items("tasks").Select(task => task.Str("id")));
        Assert.DoesNotContain(other.Id, answer.Items("tasks").Select(task => task.Str("id")));
    }

    [Fact]
    public async Task search_tasks_can_stay_within_one_tag()
    {
        await using var app = new McpHarness();
        var tagged = app.AddTask("Red folder", "", "Work");
        app.AddTask("Red shoes", "", "Home");

        var answer = await app.OkAsync("search_tasks", new { text = "red", tag = "work" });

        Assert.Equal([tagged.Id], answer.Items("tasks").Select(task => task.Str("id")));
    }

    [Fact]
    public async Task list_tags_answers_the_tags_in_use_with_counts_and_colors_in_the_users_order()
    {
        await using var app = new McpHarness();
        app.AddTask("One", "", "Work");
        app.AddTask("Two", "", "Work");
        app.AddTask("Three", "", "Home");
        app.TagColors.Seed("Work", "red");
        app.TagOrder.Seed("Home", "Work");

        var answer = await app.OkAsync("list_tags");

        var tags = answer.Items("tags");
        Assert.Equal(["Home", "Work"], tags.Select(tag => tag.Str("tag")));
        Assert.Equal([1, 2], tags.Select(tag => tag.GetProperty("count").GetInt32()));
        Assert.True(tags[0].IsAbsent("color"));
        Assert.Equal("red", tags[1].Str("color"));
    }

    [Fact]
    public async Task get_task_link_answers_the_address_to_put_in_a_description()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Linked");

        var answer = await app.OkAsync("get_task_link", new { id = task.Id });

        Assert.Equal(app.Tasks.LinkAddress(task.Id), answer.Str("address"));
    }

    [Theory]
    [InlineData("list_tasks")]
    [InlineData("search_tasks")]
    [InlineData("list_tags")]
    public async Task Every_task_read_fails_with_a_message_to_choose_a_repository_when_no_clone_is_selected(string tool)
    {
        await using var app = new McpHarness(cloneSelected: false);

        var error = await app.ErrorAsync(tool, new { text = "anything" });

        Assert.Contains("Configure", error);
    }

    [Fact]
    public async Task get_task_fails_with_a_message_to_choose_a_repository_when_no_clone_is_selected()
    {
        await using var app = new McpHarness(cloneSelected: false);

        var error = await app.ErrorAsync("get_task", new { id = "any" });

        Assert.Contains("Configure", error);
    }

    [Fact]
    public async Task Reads_change_nothing()
    {
        await using var app = new McpHarness();
        var task = app.AddTask("Stay as it is", "Same");
        var before = app.Files.AllFilesUnder(McpHarness.Clone).Count;
        var changes = 0;
        app.Tasks.Changed += (_, _) => changes++;

        await app.OkAsync("list_tasks");
        await app.OkAsync("get_task", new { id = task.Id });
        await app.OkAsync("search_tasks", new { text = "same" });
        await app.OkAsync("list_tags");

        Assert.Equal(0, changes);
        Assert.Equal(before, app.Files.AllFilesUnder(McpHarness.Clone).Count);
    }
}
