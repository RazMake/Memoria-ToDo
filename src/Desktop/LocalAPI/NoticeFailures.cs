using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// Turns a change a store saved before a notice failed into a normal call: a service runs its store call through
/// <see cref="Saved(Func{TaskItem})"/> or <see cref="Saved(Action)"/> and answers as after any save, while the failure waits here for
/// <see cref="CallInterceptor"/>, which logs it once at Error when the call ends. The list is per call and flows with it, so the
/// handler keeps the web server's own call context.
/// </summary>
internal static class NoticeFailures
{
    private static readonly AsyncLocal<List<NoticeFailedException>?> Current = new();

    /// <summary>Starts an empty list for the call that is starting on this flow, and returns it.</summary>
    public static List<NoticeFailedException> Begin()
    {
        var failures = new List<NoticeFailedException>();
        Current.Value = failures;
        return failures;
    }

    /// <summary>Runs a store call that returns the task it saved; after a notice failed, returns the task the failure holds.</summary>
    public static TaskItem Saved(Func<TaskItem> save)
    {
        try
        {
            return save();
        }
        catch (NoticeFailedException<TaskItem> failure) when (failure.Saved is not null)
        {
            Current.Value?.Add(failure);
            return failure.Saved;
        }
    }

    /// <summary>Runs a store call that saves no item; a notice failure after the save leaves it done.</summary>
    public static void Saved(Action save)
    {
        try
        {
            save();
        }
        catch (NoticeFailedException failure)
        {
            Current.Value?.Add(failure);
        }
    }
}
