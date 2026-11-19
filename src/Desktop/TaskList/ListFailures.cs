using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The failures a Tasks call from the list is known to throw: its own, then the platform's for any store call.</summary>
internal static class ListFailures
{
    /// <summary>The message for a known failure, or null when <paramref name="failure"/> isn't one the list knows.</summary>
    public static string? MessageOf(Exception failure) => failure switch
    {
        TaskNotFoundException => "This task no longer exists.",
        _ => StoreFailures.MessageOf(failure),
    };

    /// <summary>
    /// Runs the call. A known failure is passed to <paramref name="report"/> with its message and answered with false; any
    /// other failure is not swallowed.
    /// </summary>
    public static bool Run(Action call, Action<string> report) => StoreFailures.Run(call, MessageOf, report);
}
