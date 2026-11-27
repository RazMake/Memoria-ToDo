using Android.Util;
using Serilog.Core;
using Serilog.Events;

namespace Memoria.ToDo.Android.App;

/// <summary>Writes each log entry to Android's system log, tagged with the component that wrote it.</summary>
internal sealed class LogcatSink : ILogEventSink
{
    private const string FallbackTag = "App";

    public void Emit(LogEvent logEvent)
    {
        try
        {
            var message = logEvent.RenderMessage();
            if (logEvent.Exception is not null)
            {
                message += Environment.NewLine + logEvent.Exception;
            }

            Log.WriteLine(PriorityOf(logEvent.Level), TagOf(logEvent), message);
        }
        catch
        {
            // A log that can't be written never stops the app.
        }
    }

    private static string TagOf(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var value) && value is ScalarValue { Value: string tag }
            ? tag
            : FallbackTag;

    private static LogPriority PriorityOf(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogPriority.Verbose,
        LogEventLevel.Debug => LogPriority.Debug,
        LogEventLevel.Information => LogPriority.Info,
        LogEventLevel.Warning => LogPriority.Warn,
        LogEventLevel.Error => LogPriority.Error,
        _ => LogPriority.Assert,
    };
}
