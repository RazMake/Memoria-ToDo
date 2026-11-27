using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The failures a Tasks call from the list is known to throw: its own, the Task editor's words for several at once, then the platform's for any store call. A change saved before a notice failed isn't one: <see cref="StoreFailures.Run"/> words it.</summary>
internal static class ListFailures
{
    /// <summary>The message for a known failure, or null when <paramref name="failure"/> isn't one the list knows.</summary>
    public static string? MessageOf(Exception failure) => failure switch
    {
        TaskNotFoundException => "This task no longer exists.",
        AggregateException incomplete => SaveFailureText.Incomplete(incomplete),
        _ => StoreFailures.MessageOf(failure),
    };

    /// <summary>
    /// Runs the call. A known failure is logged at Error, passed to <paramref name="report"/> with its message and answered
    /// with false; a change saved before a notice failed is logged, reported with the shared message and answered with true;
    /// any other failure is not swallowed.
    /// </summary>
    public static bool Run(Action call, Action<string> report, ILogger logger) => StoreFailures.Run(call, MessageOf, report, logger);
}
