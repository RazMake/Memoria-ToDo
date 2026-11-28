namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// A folder watcher that watches nothing: it records each folder it was asked to watch and gives the test
/// the watch, so the test can report changes through it and see whether it was disposed.
/// </summary>
internal sealed class FakeFolderWatcher : IFolderWatcher
{
    private readonly List<Watch> watches = [];

    /// <summary>When set, <see cref="Watch"/> throws it instead of starting a watch.</summary>
    public Exception? FailToStart { get; set; }

    /// <summary>Every watch started so far, oldest first.</summary>
    public IReadOnlyList<Watch> Watches => watches;

    /// <summary>The folders every call asked to watch, oldest first, including a call that failed to start.</summary>
    public List<string> Folders { get; } = [];

    IDisposable IFolderWatcher.Watch(string folder, IFolderChanges changes)
    {
        Folders.Add(folder);
        if (FailToStart is { } failure)
            throw failure;
        var watch = new Watch(folder, changes);
        watches.Add(watch);
        return watch;
    }

    /// <summary>One started watch.</summary>
    public sealed class Watch(string folder, IFolderChanges changes) : IDisposable
    {
        public string Folder { get; } = folder;

        public bool IsDisposed { get; private set; }

        /// <summary>Reports a created, changed, deleted, or renamed file, as the real watcher does; it still reports after disposal, as a watcher's last events might.</summary>
        public void Changed(string relativePath) => changes.Changed(relativePath);

        public void Overflowed() => changes.Overflowed();

        public void Failed(Exception error) => changes.Failed(error);

        public void Dispose() => IsDisposed = true;
    }
}
