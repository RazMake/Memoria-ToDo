using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.Configure;

/// <summary>Makes the settings screen.</summary>
public static class ConfigureScreen
{
    /// <summary>
    /// Prepares Configure. Its <see cref="IConfigureScreen.Show"/> asks <paramref name="screens"/> to open
    /// Configure's activity. Throws <see cref="ArgumentNullException"/> when an argument is null.
    /// </summary>
    public static IConfigureScreen Create(IScreenOpener screens, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentNullException.ThrowIfNull(logger);
        return new OpeningConfigureScreen(screens);
    }
}
