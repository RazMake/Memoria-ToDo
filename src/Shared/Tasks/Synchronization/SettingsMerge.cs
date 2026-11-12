namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Merges two versions of the shared settings, each setting by its own time. Pure: no I/O.</summary>
internal static class SettingsMerge
{
    /// <summary>Null means the file is absent. Throws <see cref="InvalidDataException"/> for any present file that can't be read.</summary>
    public static byte[]? Merge(byte[]? basis, byte[]? localBytes, byte[]? remoteBytes)
    {
        if (basis is not null)
            SettingsFileFormat.Read(basis);
        var local = localBytes is null ? null : SettingsFileFormat.Read(localBytes);
        var remote = remoteBytes is null ? null : SettingsFileFormat.Read(remoteBytes);
        if (local is null || remote is null)
            return (local ?? remote) is { } only ? SettingsFileFormat.Write(only) : null;

        return SettingsFileFormat.Write(new SharedSettings(
            Newer(local.DeletionDelayDays, remote.DeletionDelayDays),
            Newer(local.AddNewTaskShortcut, remote.AddNewTaskShortcut),
            Newer(local.ManageTasksShortcut, remote.ManageTasksShortcut)));
    }

    private static Setting<T> Newer<T>(Setting<T> local, Setting<T> remote) => local.Updated > remote.Updated ? local : remote;
}
