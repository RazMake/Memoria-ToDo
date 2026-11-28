using Memoria.ToDo.Android.TaskList;

namespace Memoria.ToDo.Android.Shell;

/// <summary>The ways into the app. See docs/architecture/android/shell.md, "Public interface".</summary>
public static class AppEntry
{
    /// <summary>
    /// Connects opening the app to Task View. The composition root calls it once, last, before Android opens any
    /// screen. Throws <see cref="ArgumentNullException"/> when an argument is null.
    /// </summary>
    public static void Connect(ITaskViewScreen taskView, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(taskView);
        ArgumentNullException.ThrowIfNull(logger);
        EntryServices.Current.Set(new AppOpening(taskView, logger));
    }
}
