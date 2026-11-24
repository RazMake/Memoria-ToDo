using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using ModelContextProtocol;
using Serilog;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>
/// Runs every tool call the same way: refuses it once the server is stopping, counts it so a stop can wait for it, cancels it
/// at a stop, turns what the call returns into the JSON the assistant gets, logs the outcome, and turns a failure into a message
/// the assistant can act on. Tool methods only say what to run.
/// </summary>
internal sealed class ToolRunner(ILogger logger)
{
    /// <summary>What the assistant is told when a call comes while the server is closing or the switch just turned off.</summary>
    public const string ClosingMessage = "The MCP server is closing, so it didn't run this tool.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(), new ContactTimeConverter() },
    };

    private readonly ActiveWork _calls = new();
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>The assistant's view of a value: the JSON text a tool answers with.</summary>
    public static string ToJson(object value) => JsonSerializer.Serialize(value, Json);

    /// <summary>
    /// Refuses new calls, cancels the calls that can be cancelled, and completes when every call has ended.
    /// </summary>
    public Task StopAsync()
    {
        var idle = _calls.CloseAsync();
        _stopping.Cancel();
        return idle;
    }

    /// <summary>Runs a call that doesn't wait. <paramref name="ids"/> are the task or contact IDs it names, for the log and for "not found" messages.</summary>
    public Task<string> Run(Func<object> call, string?[]? ids = null, Func<object, string?>? resultId = null, [CallerMemberName] string tool = "") =>
        RunAsync(CancellationToken.None, _ => Task.FromResult(call()), ids, resultId, tool);

    /// <summary>
    /// Runs a call that waits and can be cancelled by <paramref name="requestToken"/> or by the stop. <paramref name="resultId"/>
    /// reads the ID the call made, such as a new task's, from its result, for the log.
    /// </summary>
    public async Task<string> RunAsync(CancellationToken requestToken, Func<CancellationToken, Task<object>> call, string?[]? ids = null, Func<object, string?>? resultId = null, [CallerMemberName] string method = "")
    {
        var tool = SnakeCase(method);
        var named = ids is null ? [] : ids.Where(id => id is not null).Cast<string>().ToArray();
        var idText = string.Join(", ", named);
        ToolOutcome.Reached();
        if (!_calls.TryEnter())
        {
            logger.Debug("Tool {Tool} was refused because the server is closing", tool);
            throw new McpException(ClosingMessage);
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestToken, _stopping.Token);
            var result = await call(linked.Token).ConfigureAwait(false);
            var made = resultId?.Invoke(result);
            var logged = made is null ? idText : idText.Length == 0 ? made : $"{idText}, {made}";
            if (logged.Length == 0)
            {
                logger.Information("Tool {Tool} done", tool);
            }
            else
            {
                logger.Information("Tool {Tool} done, IDs: {Ids}", tool, logged);
            }

            return ToJson(result);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            logger.Debug("Tool {Tool} was cancelled because the server is closing", tool);
            throw new McpException("The MCP server is closing, so this tool was cancelled.");
        }
        catch (OperationCanceledException)
        {
            logger.Debug("Tool {Tool} was cancelled by the assistant", tool);
            throw;
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception failure) when (RefusalOf(failure, idText) is { } reason)
        {
            // Never the reason: it can quote what the assistant sent, such as an alias another contact holds.
            logger.Warning("Tool {Tool} refused, IDs: {Ids}, error: {Error}", tool, idText, failure.GetType().Name);
            throw new McpException(reason);
        }
        catch (Exception failure)
        {
            // The exception goes to the entry for its trace; the message text is the assistant's, not the entry's.
            logger.Error(failure, "Tool {Tool} failed, IDs: {Ids}, error: {Error}", tool, idText, failure.GetType().Name);
            throw new McpException(failure.Message);
        }
        finally
        {
            _calls.Exit();
        }
    }

    // What the assistant did wrong or must fix first: not a fault of the server, so it is a Warning.
    private static string? RefusalOf(Exception failure, string ids) => failure switch
    {
        TaskNotFoundException => ids.Length == 0 ? "No such task." : $"No task has the ID {ids}.",
        ImageTooLargeException => "The image can't be shrunk to 1 MB.",
        StaleTaskException => "The task changed meanwhile. Read it again with get_task and retry.",
        NoRepositoryFolderException or ArgumentException or InvalidDataException => StoreFailures.MessageOf(failure),
        _ => null,
    };

    // The tool's name, such as list_tasks for ListTasks, which is also the name the SDK gives the tool.
    private static string SnakeCase(string method) =>
        string.Concat(method.Select((letter, index) => char.IsUpper(letter) ? (index == 0 ? "" : "_") + char.ToLowerInvariant(letter) : letter.ToString()));
}
