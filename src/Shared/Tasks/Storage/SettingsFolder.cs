using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The <c>settings.json</c> file in a clone.</summary>
internal sealed class SettingsFolder(IFileSystem files)
{
    /// <summary>Never throws for a bad file: a missing file gives the defaults, an unreadable one a <see cref="FileLoad{T}.Failure"/>.</summary>
    public FileLoad<SharedSettings> Read(string clone) =>
        FileLoad<SharedSettings>.Read(files, RepositoryLayout.SettingsFile(clone), SettingsFileFormat.Read, SharedSettings.Defaults);

    public void Write(string clone, SharedSettings settings) =>
        files.WriteFile(RepositoryLayout.SettingsFile(clone), SettingsFileFormat.Write(settings));
}
