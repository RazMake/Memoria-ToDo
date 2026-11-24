using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>
/// The filter around every tool call of one connection. It refuses a call that arrives once the connection is ending, so no
/// change gets in after the switch turned off, and it logs at Warning a call the SDK refused while binding its input, which no
/// tool ran and so no tool logged.
/// </summary>
internal sealed class ToolCallGate(CancellationToken ending, ILogger logger)
{
    /// <summary>Adds the filter to the connection's options.</summary>
    public void Watch(McpServerOptions options) => options.Filters.Request.CallToolFilters.Add(next => async (context, cancellationToken) =>
    {
        var tool = context.Params?.Name ?? "";
        if (ending.IsCancellationRequested)
        {
            logger.Debug("Tool {Tool} was refused because its connection is ending", tool);
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = ToolRunner.ClosingMessage }] };
        }

        var reached = ToolOutcome.Watch();
        try
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);
            if (result.IsError == true && !reached.Value)
            {
                logger.Warning("Tool {Tool} refused its input before it ran", tool);
            }

            return result;
        }
        catch (Exception e) when (e is not OperationCanceledException && !reached.Value)
        {
            logger.Warning("Tool {Tool} refused its input before it ran, error: {Error}", tool, e.GetType().Name);
            throw;
        }
    });
}
