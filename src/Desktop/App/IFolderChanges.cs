namespace Memoria.ToDo.Desktop.App;

/// <summary>What a watch of a folder reports, on a background thread.</summary>
internal interface IFolderChanges
{
    /// <summary>A file was created, changed, deleted, or renamed; for a rename, its new path. The path is relative to the watched folder.</summary>
    void Changed(string relativePath);

    /// <summary>The watcher's buffer overflowed, so changes were lost.</summary>
    void Overflowed();

    /// <summary>The watch failed and stopped reporting.</summary>
    void Failed(Exception error);
}
