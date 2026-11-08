using System.IO;
using Velopack.Locators;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Velopack's starter: the stub in the install folder's root, whose path survives updates.</summary>
internal static class InstalledStarter
{
    /// <summary>The starter's path, or null when this copy wasn't installed by Velopack, such as one run from a build folder.</summary>
    public static string? Find()
    {
        var locator = VelopackLocator.Current;
        return locator.CurrentlyInstalledVersion is null
            ? null
            : Path.Combine(locator.RootAppDir!, Path.GetFileName(locator.ThisExeRelativePath!));
    }
}
