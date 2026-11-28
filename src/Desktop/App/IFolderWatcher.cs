namespace Memoria.ToDo.Desktop.App;

/// <summary>Watches every file under a folder, subfolders included, and reports changes on a background thread.</summary>
internal interface IFolderWatcher
{
    /// <summary>Starts watching <paramref name="folder"/>; dispose the result to stop. Throws when the watch can't start.</summary>
    IDisposable Watch(string folder, IFolderChanges changes);
}
