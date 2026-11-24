using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// Runs around every call: answers <c>UNAVAILABLE</c> once the gate is closed, turns a failure into its status by the
/// <see cref="StatusMap"/>, and logs the one entry that ends the call. A stream also logs when it opens, and ends when the gate's stop begins.
/// </summary>
internal sealed class CallInterceptor : Interceptor
{
    private const string CallTemplate = "Call {Method} {Ids} ended with {Status} in {DurationMs} ms";
    private const string StreamTemplate = "Stream {Method} {Ids} closed with {Status} after {DurationMs} ms";

    private readonly ICallGate gate;
    private readonly ILogger logger;

    public CallInterceptor(ICallGate gate, ILogger logger)
    {
        this.gate = gate;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (!gate.AcceptsCalls)
                throw AppStopping();

            var response = await continuation(request, context);
            Log(CallTemplate, context, request, StatusCode.OK, null, started);
            return response;
        }
        catch (Exception failure)
        {
            var ended = failure as RpcException ?? new RpcException(StatusMap.Of(failure));
            Log(CallTemplate, context, request, ended.StatusCode, failure, started);
            if (ReferenceEquals(ended, failure))
                throw;
            throw ended;
        }
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (!gate.AcceptsCalls)
                throw AppStopping();

            // ChangeStream ends the handler when its caller cancels or the app stops, whichever comes first.
            AppStop.Token = gate.Stopping;
            logger.Information("Stream {Method} {Ids} opened", MethodOf(context), RequestIds.Of(request));
            await continuation(request, responseStream, context);
            Log(StreamTemplate, context, request, StatusCode.OK, null, started);
        }
        catch (Exception failure)
        {
            // A stream the stop cut short ends as the app stopping, not as a cancel by its caller.
            var stopped = failure is OperationCanceledException && gate.Stopping.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested;
            var ended = stopped ? AppStopping() : failure as RpcException ?? new RpcException(StatusMap.Of(failure));
            Log(StreamTemplate, context, request, ended.StatusCode, failure, started);
            if (ReferenceEquals(ended, failure))
                throw;
            throw ended;
        }
    }

    private static RpcException AppStopping() => RpcFailure.Of(StatusCode.Unavailable, "The app is stopping.");

    private static string MethodOf(ServerCallContext context) => context.Method[(context.Method.LastIndexOf('/') + 1)..];

    // Information for a call that ended as expected, was cancelled, or met the app's own stop; Warning for a status the program
    // caused; Error, with the exception, for a failure inside the app.
    private void Log(string template, ServerCallContext context, object request, StatusCode status, Exception? failure, long started)
    {
        var level = status switch
        {
            StatusCode.OK or StatusCode.Cancelled or StatusCode.Unavailable => LogEventLevel.Information,
            StatusCode.Internal or StatusCode.Unknown => LogEventLevel.Error,
            _ => LogEventLevel.Warning,
        };
        if (!logger.IsEnabled(level))
            return;

        var method = MethodOf(context);
        var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (level == LogEventLevel.Error)
            logger.Error(failure, template, method, RequestIds.Of(request), status, duration);
        else if (level == LogEventLevel.Warning)
            logger.Warning(template, method, RequestIds.Of(request), status, duration);
        else
            logger.Information(template, method, RequestIds.Of(request), status, duration);
    }
}
