using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>
/// Tracks the requests of one connection that haven't been answered yet, so the connection can be ended only after every
/// answer, including the one for a call that was cancelled, has been sent.
/// </summary>
internal sealed class PendingRequests
{
    private const string CancelledMethod = "notifications/cancelled";

    private readonly Lock _gate = new();
    private readonly HashSet<string> _ids = [];
    private TaskCompletionSource? _idle;

    /// <summary>Adds the filters that count requests in and answers out.</summary>
    public void Watch(McpServerOptions options)
    {
        options.Filters.Message.IncomingFilters.Add(next => (context, cancellationToken) =>
        {
            switch (context.JsonRpcMessage)
            {
                case JsonRpcRequest request:
                    Add(request.Id.ToString());
                    break;
                case JsonRpcNotification { Method: CancelledMethod, Params: { } cancelled }
                    when cancelled["requestId"] is { } id:
                    // The SDK answers a cancelled call only sometimes, so a cancelled one stops being awaited.
                    Remove(id.ToString());
                    break;
            }

            return next(context, cancellationToken);
        });
        options.Filters.Message.OutgoingFilters.Add(next => async (context, cancellationToken) =>
        {
            try
            {
                await next(context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (context.JsonRpcMessage is JsonRpcResponse or JsonRpcError)
                {
                    Remove(((JsonRpcMessageWithId)context.JsonRpcMessage).Id.ToString());
                }
            }
        });
    }

    /// <summary>Completes when no request is waiting for its answer.</summary>
    public Task WhenIdle()
    {
        lock (_gate)
        {
            if (_ids.Count == 0)
            {
                return Task.CompletedTask;
            }

            return (_idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
    }

    private void Add(string id)
    {
        lock (_gate)
        {
            _ids.Add(id);
        }
    }

    private void Remove(string id)
    {
        TaskCompletionSource? idle = null;
        lock (_gate)
        {
            if (_ids.Remove(id) && _ids.Count == 0)
            {
                idle = _idle;
                _idle = null;
            }
        }

        idle?.TrySetResult();
    }
}
