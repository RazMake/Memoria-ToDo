using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.McpServer.Tests;

/// <summary>The copy-ready connection lines Configure shows for each assistant, and the checks Create makes of its arguments.</summary>
[Unit]
public partial class McpConnectionsTests
{
    private static string Escaped(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private static bool HoldsBridgePath(string text) =>
        text.Contains(McpHarness.BridgePath, StringComparison.Ordinal) || text.Contains(Escaped(McpHarness.BridgePath), StringComparison.Ordinal);

    [Fact]
    public void Offers_one_line_each_for_VS_Code_Claude_Desktop_Claude_Code_and_Codex_in_that_order()
    {
        var app = new McpHarness();

        var assistants = app.Server.Connections.Select(connection => connection.Assistant).ToList();

        Assert.Equal(4, assistants.Count);
        Assert.Contains("VS Code", assistants[0]);
        Assert.Contains("Claude Desktop", assistants[1]);
        Assert.Contains("Claude Code", assistants[2]);
        Assert.Contains("Codex", assistants[3]);
    }

    [Fact]
    public void Every_line_holds_the_path_of_the_bridge_and_asks_it_to_run_as_an_MCP_bridge()
    {
        var app = new McpHarness();

        Assert.All(app.Server.Connections, connection =>
        {
            Assert.True(HoldsBridgePath(connection.Text), $"{connection.Assistant}: {connection.Text}");
            Assert.Contains("--mcp", connection.Text);
        });
    }

    [Fact]
    public void The_bridge_argument_is_the_one_the_app_recognizes()
    {
        Assert.Equal("--mcp", McpServer.BridgeArgument);
    }

    [Fact]
    public void Every_text_holds_the_bridge_argument()
    {
        var app = new McpHarness();

        Assert.All(app.Server.Connections, connection =>
            Assert.True(connection.Text.Contains(McpServer.BridgeArgument, StringComparison.Ordinal), $"{connection.Assistant}: {connection.Text}"));
    }

    [Fact]
    public void The_VS_Code_text_is_one_JSON_document_that_runs_the_bridge()
    {
        var app = new McpHarness();

        using var document = JsonDocument.Parse(TextOf(app, "VS Code"));

        var server = document.RootElement.GetProperty("servers").EnumerateObject().Single().Value;
        Assert.Equal(McpHarness.BridgePath, server.GetProperty("command").GetString());
        Assert.Equal(new[] { McpServer.BridgeArgument }, server.GetProperty("args").EnumerateArray().Select(argument => argument.GetString()));
    }

    [Fact]
    public void The_Claude_Desktop_text_is_one_JSON_document_that_runs_the_bridge()
    {
        var app = new McpHarness();

        using var document = JsonDocument.Parse(TextOf(app, "Claude Desktop"));

        var server = document.RootElement.GetProperty("mcpServers").EnumerateObject().Single().Value;
        Assert.Equal(McpHarness.BridgePath, server.GetProperty("command").GetString());
        Assert.Equal(new[] { McpServer.BridgeArgument }, server.GetProperty("args").EnumerateArray().Select(argument => argument.GetString()));
    }

    [Fact]
    public void The_Claude_Code_text_is_a_single_command_line()
    {
        var app = new McpHarness();

        var text = TextOf(app, "Claude Code").Trim();

        Assert.DoesNotContain('\n', text);
        Assert.StartsWith("claude mcp add", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Codex_text_has_only_table_headers_key_value_lines_and_blank_lines()
    {
        var app = new McpHarness();

        var lines = TextOf(app, "Codex").Split('\n').Select(line => line.Trim()).ToList();

        Assert.Contains(lines, line => TomlTable().IsMatch(line));
        Assert.All(lines, line => Assert.True(
            line.Length == 0 || TomlTable().IsMatch(line) || TomlPair().IsMatch(line), $"Not TOML: {line}"));
    }

    [GeneratedRegex(@"^\[[A-Za-z0-9_.\-]+\]$")]
    private static partial Regex TomlTable();

    [GeneratedRegex(@"^[A-Za-z0-9_\-]+\s*=\s*\S.*$")]
    private static partial Regex TomlPair();

    private static string TextOf(McpHarness app, string assistant) =>
        app.Server.Connections.Single(connection => connection.Assistant.Contains(assistant, StringComparison.Ordinal)).Text;

    [Fact]
    public void Follows_the_bridge_path_it_was_given()
    {
        var app = new McpHarness();
        var other = app.Create(bridgePath: @"D:\Other place\Memoria.ToDo.Desktop.App.exe");

        Assert.All(other.Connections, connection => Assert.Contains("Other place", connection.Text));
        Assert.All(other.Connections, connection => Assert.DoesNotContain("Apps", connection.Text));
    }

    [Fact]
    public void Gives_the_same_lines_every_time()
    {
        var app = new McpHarness();

        Assert.Equal(app.Server.Connections, app.Server.Connections);
    }

    [Fact]
    public void Create_refuses_a_relative_device_folder()
    {
        var app = new McpHarness();

        Assert.Throws<ArgumentException>(() => app.Create(deviceFolder: @"relative\device"));
    }

    [Fact]
    public void Create_refuses_a_relative_bridge_path()
    {
        var app = new McpHarness();

        Assert.Throws<ArgumentException>(() => app.Create(bridgePath: @"relative\app.exe"));
    }

    [Fact]
    public void Create_refuses_an_empty_pipe_name()
    {
        var app = new McpHarness();

        Assert.ThrowsAny<ArgumentException>(() => McpServer.Create(
            app.Tasks, app.TagColors, app.TagOrder, app.Settings, app.Contacts, app.Sync, app.Updates, app.StartWithWindows,
            app.Logs, app.Files, McpHarness.DeviceFolder, "", McpHarness.BridgePath, app.Time, app.Log.CreateLogger()));
    }

    [Fact]
    public void Create_refuses_a_missing_logger()
    {
        var app = new McpHarness();

        var failure = Assert.Throws<ArgumentNullException>(() => McpServer.Create(
            app.Tasks, app.TagColors, app.TagOrder, app.Settings, app.Contacts, app.Sync, app.Updates, app.StartWithWindows,
            app.Logs, app.Files, McpHarness.DeviceFolder, app.PipeName, McpHarness.BridgePath, app.Time, null!));

        Assert.Equal("logger", failure.ParamName);
    }

    [Fact]
    public void Create_refuses_a_missing_task_module()
    {
        var app = new McpHarness();

        var failure = Assert.Throws<ArgumentNullException>(() => McpServer.Create(
            null!, app.TagColors, app.TagOrder, app.Settings, app.Contacts, app.Sync, app.Updates, app.StartWithWindows,
            app.Logs, app.Files, McpHarness.DeviceFolder, app.PipeName, McpHarness.BridgePath, app.Time, app.Log.CreateLogger()));

        Assert.Equal("tasks", failure.ParamName);
    }
}
