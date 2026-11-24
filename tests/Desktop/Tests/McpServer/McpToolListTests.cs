using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.McpServer.Tests;

/// <summary>The tools the server offers, by the design's table: which exist, which are read-only or destructive, and which operations get none.</summary>
[Unit]
public class McpToolListTests
{
    private static readonly string[] ReadTools =
    [
        "list_tasks", "get_task", "search_tasks", "list_tags", "get_task_link",
        "get_deletion_delay", "get_start_with_windows",
        "list_contacts", "find_contact", "search_contacts", "get_reporting_line", "get_contact_mention",
        "get_sync_status", "get_update_status", "read_log",
    ];

    private static readonly string[] DestructiveTools = ["delete_task", "delete_contact", "set_deletion_delay"];

    private static readonly string[] OtherChangeTools =
    [
        "add_task", "edit_task", "add_image", "move_task", "resolve_task", "reopen_task",
        "set_tag_color", "move_tag", "set_start_with_windows",
        "save_contact", "move_contact_group",
        "sync_now", "set_device_name", "set_auto_update",
    ];

    private static async Task<IList<ModelContextProtocol.Client.McpClientTool>> ToolsAsync(McpHarness app)
    {
        var client = await app.ConnectAsync();
        return await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask().WaitAsync(McpHarness.Bound);
    }

    [Fact]
    public async Task Offers_exactly_the_tools_of_the_design_table()
    {
        await using var app = new McpHarness();

        var tools = await ToolsAsync(app);

        Assert.Equal(
            ReadTools.Concat(DestructiveTools).Concat(OtherChangeTools).Order(StringComparer.Ordinal),
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Names_every_tool_with_lowercase_words_joined_by_underscores()
    {
        await using var app = new McpHarness();

        var tools = await ToolsAsync(app);

        Assert.All(tools, tool => Assert.Matches("^[a-z]+(_[a-z]+)+$", tool.Name));
    }

    [Fact]
    public async Task Marks_every_read_tool_read_only()
    {
        await using var app = new McpHarness();

        var tools = (await ToolsAsync(app)).Where(tool => ReadTools.Contains(tool.Name)).ToList();

        Assert.Equal(ReadTools.Length, tools.Count);
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint == true, $"{tool.Name} isn't read-only."));
    }

    [Fact]
    public async Task Marks_no_change_tool_read_only()
    {
        await using var app = new McpHarness();

        var tools = (await ToolsAsync(app)).Where(tool => !ReadTools.Contains(tool.Name)).ToList();

        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint != true, $"{tool.Name} is read-only."));
    }

    [Fact]
    public async Task Marks_deleting_and_changing_the_deletion_delay_destructive()
    {
        await using var app = new McpHarness();

        var tools = (await ToolsAsync(app)).Where(tool => DestructiveTools.Contains(tool.Name)).ToList();

        Assert.Equal(DestructiveTools.Length, tools.Count);
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.DestructiveHint == true, $"{tool.Name} isn't destructive."));
    }

    [Fact]
    public async Task Marks_every_other_change_explicitly_not_destructive()
    {
        await using var app = new McpHarness();

        var tools = (await ToolsAsync(app)).Where(tool => OtherChangeTools.Contains(tool.Name)).ToList();

        Assert.Equal(OtherChangeTools.Length, tools.Count);
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.DestructiveHint == false, $"{tool.Name} isn't marked not destructive."));
    }

    [Theory]
    [InlineData("ssh")]
    [InlineData("key")]
    [InlineData("clone")]
    [InlineData("repository")]
    [InlineData("trust")]
    [InlineData("host")]
    [InlineData("update_now")]
    [InlineData("shortcut")]
    [InlineData("draft")]
    [InlineData("save_image")]
    [InlineData("exit")]
    public async Task Has_no_tool_for_an_excluded_operation(string word)
    {
        await using var app = new McpHarness();

        var tools = await ToolsAsync(app);

        Assert.DoesNotContain(tools, tool => tool.Name.Contains(word, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Describes_every_tool()
    {
        await using var app = new McpHarness();

        var tools = await ToolsAsync(app);

        Assert.All(tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Description), $"{tool.Name} has no description."));
    }

    [Fact]
    public async Task The_delete_task_description_says_resolving_is_the_undoable_path()
    {
        await using var app = new McpHarness();

        var tool = (await ToolsAsync(app)).Single(tool => tool.Name == "delete_task");

        Assert.Contains("resolve", tool.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_delete_contact_description_says_a_deleted_contact_cannot_be_restored()
    {
        await using var app = new McpHarness();

        var tool = (await ToolsAsync(app)).Single(tool => tool.Name == "delete_contact");

        Assert.Contains("restor", tool.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Serves_several_assistants_at_once_each_through_its_own_connection()
    {
        await using var app = new McpHarness();
        var first = await app.ConnectAsync();
        var second = await app.ConnectAsync();
        app.AddTask("Shared task");

        var fromFirst = await McpHarness.Call(first, "list_tasks");
        var fromSecond = await McpHarness.Call(second, "list_tasks");

        Assert.Equal("Shared task", fromFirst.Json().Items("tasks").Single().Str("title"));
        Assert.Equal("Shared task", fromSecond.Json().Items("tasks").Single().Str("title"));
    }
}
