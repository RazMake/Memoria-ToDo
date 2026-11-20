using System.Text.Json;
using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// <c>MentionLookup</c> finds the mention under a point of a description page through DevTools calls: the node at the point, its
/// remote object, and the mention around it. The tests stand in for DevTools with a fake that records every call and answers with
/// canned JSON or throws. They check what is asked, what comes back, what is logged, and that every remote object is released.
/// </summary>
[Unit]
public class MentionLookupTests
{
    private const string Location = """{"backendNodeId":42}""";
    private const string Node = """{"object":{"objectId":"obj-1"}}""";
    private const string Found = """{"result":{"value":[3,10,20,30,40]}}""";
    private const string NoMention = """{"result":{"value":null}}""";

    private sealed class FakeDevTools : IDevToolsCalls
    {
        private readonly Dictionary<string, Func<JsonElement>> _answers = [];

        public List<(string Method, string Parameters)> Calls { get; } = [];

        public FakeDevTools Answers(string method, string json)
        {
            _answers[method] = () => JsonDocument.Parse(json).RootElement.Clone();
            return this;
        }

        private readonly Dictionary<string, Action> _onCall = [];

        public void OnCall(string method, Action action) => _onCall[method] = action;

        public FakeDevTools Throws(string method, string message)
        {
            _answers[method] = () => throw new InvalidOperationException(message);
            return this;
        }

        public IEnumerable<string> Methods => Calls.Select(call => call.Method);

        public string ParametersOf(string method) => Calls.First(call => call.Method == method).Parameters;

        public int GetInt(string method, string name) => JsonDocument.Parse(ParametersOf(method)).RootElement.GetProperty(name).GetInt32();

        public Task<JsonElement> CallAsync(string method, string parametersJson)
        {
            Calls.Add((method, parametersJson));
            if (_onCall.TryGetValue(method, out var action))
            {
                action();
            }

            return Task.FromResult(_answers.TryGetValue(method, out var answer) ? answer() : JsonDocument.Parse("{}").RootElement.Clone());
        }
    }

    private static FakeDevTools PageWith(string callFunctionOnAnswer) => new FakeDevTools()
        .Answers("DOM.getNodeForLocation", Location)
        .Answers("DOM.resolveNode", Node)
        .Answers("Runtime.callFunctionOn", callFunctionOnAnswer);

    private static (MentionLookup Lookup, InMemoryLogSink Log) LookupOn(FakeDevTools devTools, Func<bool>? isOutdated = null)
    {
        var log = new InMemoryLogSink();
        return (new MentionLookup(devTools, log.CreateLogger(), isOutdated ?? (() => false)), log);
    }

    [Fact]
    public async Task Finds_the_number_and_box_of_the_mention_around_the_node_at_the_point()
    {
        var (lookup, _) = LookupOn(PageWith(Found));

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Equal(new MentionAt(3, new Rect(10, 20, 30, 40)), found);
    }

    [Fact]
    public async Task Asks_DevTools_for_the_node_then_its_object_then_the_mention_in_that_order()
    {
        var devTools = PageWith(Found);
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Equal(["DOM.getNodeForLocation", "DOM.resolveNode", "Runtime.callFunctionOn", "Runtime.releaseObject"], devTools.Methods);
    }

    [Fact]
    public async Task Sends_the_floored_point_unchanged_at_zoom_1()
    {
        var devTools = PageWith(Found);
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15.9, 25.2), 1);

        Assert.Equal(15, devTools.GetInt("DOM.getNodeForLocation", "x"));
        Assert.Equal(25, devTools.GetInt("DOM.getNodeForLocation", "y"));
    }

    [Fact]
    public async Task Sends_the_point_divided_by_the_zoom_and_floored()
    {
        var devTools = PageWith(Found);
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(250.7, 130.2), 2);

        Assert.Equal(125, devTools.GetInt("DOM.getNodeForLocation", "x"));
        Assert.Equal(65, devTools.GetInt("DOM.getNodeForLocation", "y"));
    }

    [Fact]
    public async Task Returns_the_box_scaled_by_the_zoom()
    {
        var (lookup, _) = LookupOn(PageWith(Found));

        var found = await lookup.FindAsync(new Point(30, 50), 1.5);

        Assert.Equal(new MentionAt(3, new Rect(15, 30, 45, 60)), found);
    }

    [Fact]
    public async Task Resolves_the_node_the_location_named()
    {
        var devTools = PageWith(Found);
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Equal(42, devTools.GetInt("DOM.resolveNode", "backendNodeId"));
    }

    [Fact]
    public async Task Reads_the_mention_from_the_resolved_object_by_value()
    {
        var devTools = PageWith(Found);
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15, 25), 1);

        using var parameters = JsonDocument.Parse(devTools.ParametersOf("Runtime.callFunctionOn"));
        Assert.Equal("obj-1", parameters.RootElement.GetProperty("objectId").GetString());
        Assert.True(parameters.RootElement.GetProperty("returnByValue").GetBoolean());
    }

    [Fact]
    public async Task Returns_null_when_no_mention_is_around_the_node()
    {
        var (lookup, log) = LookupOn(PageWith(NoMention));

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Null(found);
        Assert.Empty(log.Events);
    }

    [Fact]
    public async Task Returns_null_and_logs_nothing_when_there_is_no_node_at_the_point()
    {
        var devTools = new FakeDevTools().Throws("DOM.getNodeForLocation", "No node found at given location");
        var (lookup, log) = LookupOn(devTools);

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Null(found);
        Assert.Empty(log.Events);
        Assert.Equal(["DOM.getNodeForLocation"], devTools.Methods);
    }

    [Theory]
    [InlineData("DOM.resolveNode")]
    [InlineData("Runtime.callFunctionOn")]
    public async Task Returns_null_and_logs_one_warning_with_the_exception_when_a_later_call_fails(string failing)
    {
        var (lookup, log) = LookupOn(PageWith(Found).Throws(failing, "boom"));

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Null(found);
        var entry = Assert.Single(log.Events);
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.Equal("boom", entry.Exception?.Message);
    }

    [Theory]
    [InlineData("DOM.resolveNode")]
    [InlineData("Runtime.callFunctionOn")]
    public async Task Returns_null_and_logs_nothing_when_a_later_call_fails_because_the_page_was_replaced(string failing)
    {
        var outdated = false;
        var devTools = PageWith(Found).Throws(failing, "Cannot find context with specified id");
        var (lookup, log) = LookupOn(devTools, () => outdated);
        devTools.OnCall("DOM.getNodeForLocation", () => outdated = true);

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Null(found);
        Assert.Empty(log.Events);
    }

    [Fact]
    public async Task Releases_the_object_when_a_mention_is_found()
    {
        var devTools = PageWith(Found);
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Equal("""{"objectId":"obj-1"}""", Assert.Single(devTools.Calls, call => call.Method == "Runtime.releaseObject").Parameters);
    }

    [Fact]
    public async Task Releases_the_object_when_no_mention_is_found()
    {
        var devTools = PageWith(NoMention);
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Equal("""{"objectId":"obj-1"}""", Assert.Single(devTools.Calls, call => call.Method == "Runtime.releaseObject").Parameters);
    }

    [Fact]
    public async Task Releases_the_object_when_reading_the_mention_fails()
    {
        var devTools = PageWith(Found).Throws("Runtime.callFunctionOn", "boom");
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Equal("""{"objectId":"obj-1"}""", Assert.Single(devTools.Calls, call => call.Method == "Runtime.releaseObject").Parameters);
    }

    [Fact]
    public async Task Releases_nothing_when_no_object_was_resolved()
    {
        var devTools = PageWith(Found).Throws("DOM.resolveNode", "boom");
        var (lookup, _) = LookupOn(devTools);

        await lookup.FindAsync(new Point(15, 25), 1);

        Assert.DoesNotContain("Runtime.releaseObject", devTools.Methods);
    }

    [Fact]
    public async Task Still_returns_the_mention_and_logs_nothing_when_releasing_the_object_fails()
    {
        var (lookup, log) = LookupOn(PageWith(Found).Throws("Runtime.releaseObject", "gone"));

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Equal(new MentionAt(3, new Rect(10, 20, 30, 40)), found);
        Assert.Empty(log.Events);
    }

    [Fact]
    public async Task Returns_null_and_logs_nothing_when_releasing_fails_after_no_mention_was_found()
    {
        var (lookup, log) = LookupOn(PageWith(NoMention).Throws("Runtime.releaseObject", "gone"));

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Null(found);
        Assert.Empty(log.Events);
    }

    [Fact]
    public async Task Logs_only_the_reading_failure_when_releasing_the_object_fails_too()
    {
        var (lookup, log) = LookupOn(PageWith(Found).Throws("Runtime.callFunctionOn", "boom").Throws("Runtime.releaseObject", "gone"));

        var found = await lookup.FindAsync(new Point(15, 25), 1);

        Assert.Null(found);
        var entry = Assert.Single(log.Events);
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.Equal("boom", entry.Exception?.Message);
    }
}
