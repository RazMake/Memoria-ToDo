namespace Memoria.ToDo.Shared.Sync;

/// <summary>Deletes a failed clone's files; Git marks its object files read-only.</summary>
internal static class FolderCleaner
{
    /// <summary>Empties the folder, and removes the folder itself too when <paramref name="removeFolder"/> is set.</summary>
    public static void Clear(string folder, bool removeFolder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        if (removeFolder)
        {
            Directory.Delete(folder);
        }
    }
}
