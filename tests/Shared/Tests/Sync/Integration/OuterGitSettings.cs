using LibGit2Sharp;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// Gives libgit2 a bad git configuration at its four outer levels (system, global, XDG, ProgramData), the way a PC whose own
/// settings break Sync does. It writes the same text as <c>gitconfig</c>, <c>.gitconfig</c> and <c>config</c> in a scratch folder,
/// the file names libgit2 looks for at those levels, and points all four levels there. Disposing restores libgit2's default
/// search paths and deletes the folder. libgit2's search paths are process-wide, so a test using this joins <see cref="Collection"/>.
/// </summary>
internal sealed class OuterGitSettings : IDisposable
{
    /// <summary>The xUnit collection of every test that changes libgit2's search paths; it runs alone, after the others.</summary>
    public const string Collection = "libgit2 outer settings";

    private static readonly ConfigurationLevel[] Levels =
        [ConfigurationLevel.System, ConfigurationLevel.Global, ConfigurationLevel.Xdg, ConfigurationLevel.ProgramData];

    private readonly string folder;

    public OuterGitSettings(string settings)
    {
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            foreach (var name in new[] { "gitconfig", ".gitconfig", "config" })
                File.WriteAllText(Path.Combine(folder, name), settings);
            foreach (var level in Levels)
                GlobalSettings.SetConfigSearchPaths(level, folder);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            foreach (var level in Levels)
                GlobalSettings.SetConfigSearchPaths(level, null);
        }
        finally
        {
            SyncWorld.DeleteScratch(folder);
        }
    }
}

/// <summary>Declares <see cref="OuterGitSettings.Collection"/> with parallelization off, so no other test runs while its tests do.</summary>
[CollectionDefinition(OuterGitSettings.Collection, DisableParallelization = true)]
public sealed class OuterGitSettingsCollection;
