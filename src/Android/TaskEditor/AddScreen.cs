using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.TaskEditor;

/// <summary>Makes the Add screen.</summary>
public static class AddScreen
{
    /// <summary>
    /// Prepares Add. Its <see cref="IAddScreen.Show"/> asks <paramref name="screens"/> to open Add's activity.
    /// Throws <see cref="ArgumentNullException"/> when an argument is null.
    /// </summary>
    public static IAddScreen Create(IScreenOpener screens, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentNullException.ThrowIfNull(logger);
        return new OpeningAddScreen(screens);
    }
}
