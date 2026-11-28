using Memoria.ToDo.Android.Platform;
using Memoria.ToDo.Android.TaskEditor;

namespace Memoria.ToDo.Android.TaskList;

/// <summary>Makes Task View.</summary>
public static class TaskViewScreen
{
    /// <summary>
    /// Prepares Task View. Its <see cref="ITaskViewScreen.Show"/> asks <paramref name="screens"/> to open Task View's
    /// activity, its floating button calls <paramref name="add"/>'s Show, and its Settings item calls
    /// <paramref name="openSettings"/>. Throws <see cref="ArgumentNullException"/> when an argument is null.
    /// </summary>
    public static ITaskViewScreen Create(IAddScreen add, Action openSettings, IScreenOpener screens, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(openSettings);
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentNullException.ThrowIfNull(logger);
        TaskViewServices.Current.Set(new TaskViewActions(add, openSettings));
        return new OpeningTaskViewScreen(screens);
    }
}
