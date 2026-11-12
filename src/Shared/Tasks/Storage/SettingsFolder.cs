using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>What reading <c>settings.json</c> found. <see cref="Failure"/> is set when the file is there but can't be read.</summary>
internal sealed record SettingsLoad(SharedSettings Settings, bool Present, InvalidDataException? Failure)
{
    /// <summary>The settings, or <see cref="InvalidDataException"/> when the file can't be read, so nothing runs on guessed values.</summary>
    public SharedSettings Require() =>
        Failure is null ? Settings : throw new InvalidDataException(Failure.Message, Failure);
}

/// <summary>The <c>settings.json</c> file in a clone.</summary>
internal sealed class SettingsFolder(IFileSystem files)
{
    /// <summary>Never throws for a bad file: a missing file gives the defaults, an unreadable one a <see cref="SettingsLoad.Failure"/>.</summary>
    public SettingsLoad Read(string clone)
    {
        var path = RepositoryLayout.SettingsFile(clone);
        try
        {
            return new SettingsLoad(SettingsFileFormat.Read(files.ReadFile(path)), true, null);
        }
        catch (FileNotFoundException)
        {
            return new SettingsLoad(SharedSettings.Defaults, false, null);
        }
        catch (InvalidDataException e)
        {
            return new SettingsLoad(SharedSettings.Defaults, true, e);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoad(SharedSettings.Defaults, true, new InvalidDataException(e.Message, e));
        }
    }

    public void Write(string clone, SharedSettings settings) =>
        files.WriteFile(RepositoryLayout.SettingsFile(clone), SettingsFileFormat.Write(settings));
}
