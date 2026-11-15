using LibGit2Sharp;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Makes libgit2 ignore the system, global, XDG, and ProgramData configuration levels, so only the clone's own settings apply.</summary>
internal static class GitSettingsIsolation
{
    private static readonly ConfigurationLevel[] OuterLevels =
    [
        ConfigurationLevel.System,
        ConfigurationLevel.Global,
        ConfigurationLevel.Xdg,
        ConfigurationLevel.ProgramData,
    ];

    /// <summary>
    /// Empties the search path of each outer level, which libgit2 reads as "no file at this level". The setting is shared by the
    /// whole process, so call this right before every git operation, on the worker that runs it. A failure propagates.
    /// </summary>
    public static void Apply()
    {
        foreach (var level in OuterLevels)
        {
            GlobalSettings.SetConfigSearchPaths(level, Array.Empty<string>());
        }
    }
}
