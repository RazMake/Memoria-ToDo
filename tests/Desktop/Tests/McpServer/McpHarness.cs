using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Memoria.ToDo.Desktop.McpServer.Tests;

/// <summary>
/// The MCP server built on the shared fakes of its ports, with the real Tasks and Contacts over the fake file system, as its
/// tests use it: a clone folder selected, the clock at 2026-11-23 20:00 UTC in a local time zone of UTC-8, and no listening.
/// A test talks to the server through the official client, joined to the server by in-process streams, never through the pipe.
/// Build it, and start the server, on the test's UI thread when the test needs the server's UI-thread rules.
/// Shared test setup, not a fake of a port.
/// </summary>
internal sealed class McpHarness : IAsyncDisposable
{
    public const string DeviceFolder = @"C:\mcp tests\device";
    public const string Clone = @"C:\mcp tests\clone";
    public const string BridgePath = @"C:\Apps\Memoria-ToDo\current\Memoria.ToDo.Desktop.App.exe";

    /// <summary>How long a test waits for the server or the client, so a lost answer fails one test instead of hanging the run.</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly List<McpClient> clients = [];
    private readonly List<Task> serving = [];
    private readonly List<PipeWriter> clientEnds = [];
    private readonly IContacts contactsOfServer;
    private McpClient? defaultClient;

    public McpHarness(bool cloneSelected = true, Func<IContacts, IContacts>? wrapContactsForServer = null)
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 23, 20, 0, 0, TimeSpan.Zero));
        Time.UseLocalTimeZone(Pacific);
        Tasks = TestTaskStore.Create(Files, Images, Time, DeviceFolder);
        Contacts = TestContactStore.Create(Files).Contacts;
        contactsOfServer = wrapContactsForServer?.Invoke(Contacts) ?? Contacts;
        if (cloneSelected)
        {
            Tasks.UseRepositoryFolder(Clone);
            Contacts.UseRepositoryFolder(Clone);
            Sync.SetupControl.Publish(Sync.SetupControl.Snapshot with { RepositoryFolder = Clone });
        }
        else
        {
            Sync.SetupControl.Publish(Sync.SetupControl.Snapshot with { RepositoryFolder = null });
        }

        PipeName = $"Memoria.ToDo.Tests.Mcp.{Guid.NewGuid():N}";
        Server = Create();
    }

    /// <summary>A local time zone eight hours behind UTC all year, so a test sees a local time that isn't UTC.</summary>
    public static TimeZoneInfo Pacific { get; } = TimeZoneInfo.CreateCustomTimeZone("Mcp-test-8", TimeSpan.FromHours(-8), "Mcp test -8", "Mcp test -8");

    public static string SwitchFile { get; } = Path.Combine(DeviceFolder, "mcp-server.txt");

    public string PipeName { get; }

    public FakeFileSystem Files { get; } = new();

    public FakeTimeProvider Time { get; }

    public FakeImageEncoder Images { get; } = new();

    public ITasks Tasks { get; }

    /// <summary>The real Contacts the tests read; the server gets it, or what <c>wrapContactsForServer</c> made of it.</summary>
    public IContacts Contacts { get; }

    public FakeTagColors TagColors { get; } = new();

    public FakeTagOrder TagOrder { get; } = new();

    public FakeSharedTaskSettings Settings { get; } = new();

    public FakeSyncService Sync { get; } = new();

    public FakeUpdateStatus Updates { get; } = new();

    public FakeStartWithWindows StartWithWindows { get; } = new();

    public FakeLogSource Logs { get; } = new();

    /// <summary>The entries the server logged, through the logger it was given.</summary>
    public InMemoryLogSink Log { get; } = new();

    public IMcpServer Server { get; }

    /// <summary>Calls <see cref="McpServer.Create"/> on these fakes, for a test that changes an argument.</summary>
    public IMcpServer Create(string? deviceFolder = null, string? bridgePath = null) =>
        McpServer.Create(Tasks, TagColors, TagOrder, Settings, contactsOfServer, Sync, Updates, StartWithWindows, Logs, Files,
            deviceFolder ?? DeviceFolder, PipeName, bridgePath ?? BridgePath, Time, Log.CreateLogger());

    /// <summary>Seeds a "team" group with the members, each made by <see cref="ContactFiles.Person"/>, and has Contacts read the clone.</summary>
    public void UseContacts(params string[] members) => Contacts.UseContacts(Files, Clone, null, members);

    /// <summary>Adds a task as Tasks does for an assistant, at the top.</summary>
    public TaskItem AddTask(string title, string description = "", string? tag = null) => Tasks.Add(title, description, tag);

    /// <summary>Writes the switch's file, as an earlier run or the user's choice would have.</summary>
    public void WriteSwitch(string text) => Files.WriteFile(SwitchFile, Encoding.UTF8.GetBytes(text));

    /// <summary>What the switch's file holds now, trimmed.</summary>
    public string ReadSwitch() => Encoding.UTF8.GetString(Files.ReadFile(SwitchFile)).Trim();

    /// <summary>Starts the server on the calling thread, with the switch off so it listens on no pipe, as unit tests must.</summary>
    public void StartOff()
    {
        WriteSwitch("off");
        Server.Start();
    }

    /// <summary>Connects another client, as another assistant would, over its own pair of streams.</summary>
    public async Task<McpClient> ConnectAsync()
    {
        var toServer = new Pipe();
        var toClient = new Pipe();
        serving.Add(((IMcpConnections)Server).ServeAsync(toServer.Reader.AsStream(), toClient.Writer.AsStream()));
        clientEnds.Add(toServer.Writer);
        var transport = new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream());
        var client = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken).WaitAsync(Bound);
        clients.Add(client);
        return client;
    }

    /// <summary>Calls the tool through the one client of this harness, which connects at the first call.</summary>
    public async Task<CallToolResult> CallAsync(string tool, object? arguments = null)
    {
        defaultClient ??= await ConnectAsync();
        return await Call(defaultClient, tool, arguments);
    }

    /// <summary>Calls the tool through <paramref name="client"/>.</summary>
    public static Task<CallToolResult> Call(McpClient client, string tool, object? arguments = null) =>
        client.CallToolAsync(tool, ArgumentsOf(arguments), cancellationToken: TestContext.Current.CancellationToken).AsTask().WaitAsync(Bound);

    /// <summary>Calls the tool, expects it to succeed, and returns the JSON it answered.</summary>
    public async Task<JsonElement> OkAsync(string tool, object? arguments = null)
    {
        var result = await CallAsync(tool, arguments);
        Assert.True(result.IsError != true, $"{tool} failed: {result.Text()}");
        return result.Json();
    }

    /// <summary>Calls the tool, expects it to fail as a tool error, and returns the error's text.</summary>
    public async Task<string> ErrorAsync(string tool, object? arguments = null)
    {
        var result = await CallAsync(tool, arguments);
        Assert.True(result.IsError == true, $"{tool} should have failed but answered: {result.Text()}");
        return result.Text();
    }

    /// <summary>
    /// Whether the call was refused, either as a tool error or as a protocol error, for an input whose type the SDK itself may
    /// refuse before the tool runs, such as a number where a name is asked.
    /// </summary>
    public async Task<bool> WasRefusedAsync(string tool, object? arguments)
    {
        try
        {
            var result = await CallAsync(tool, arguments);
            return result.IsError == true;
        }
        catch (ModelContextProtocol.McpException)
        {
            return true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in clients)
        {
            await client.DisposeAsync().AsTask().WaitAsync(Bound);
        }

        // The SDK's client leaves its streams open; closing its end is what the bridge does when the assistant goes.
        foreach (var end in clientEnds)
        {
            await end.CompleteAsync();
        }

        foreach (var task in serving)
        {
            await task.WaitAsync(Bound);
        }

        await Server.StopAsync().WaitAsync(Bound);
    }

    private static Dictionary<string, object?> ArgumentsOf(object? arguments)
    {
        var result = new Dictionary<string, object?>();
        if (arguments is not null)
        {
            foreach (var property in JsonSerializer.SerializeToElement(arguments).EnumerateObject())
            {
                result[property.Name] = property.Value.Clone();
            }
        }

        return result;
    }
}

/// <summary>What a test reads of a tool's answer: its text, as the JSON the tool returns.</summary>
internal static class McpResults
{
    public static string Text(this CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    public static JsonElement Json(this CallToolResult result)
    {
        using var document = JsonDocument.Parse(result.Text());
        return document.RootElement.Clone();
    }

    /// <summary>The text of the property, or null when it is missing or null.</summary>
    public static string? Str(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

    /// <summary>Whether the property is missing or null.</summary>
    public static bool IsAbsent(this JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;

    /// <summary>The texts of the array property.</summary>
    public static string[] Strings(this JsonElement element, string name) =>
        [.. element.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];

    /// <summary>The items of the array property.</summary>
    public static JsonElement[] Items(this JsonElement element, string name) => [.. element.GetProperty(name).EnumerateArray()];
}
