using System.Text;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The selected repository folder, kept as one text file on this device. The folder it holds is the one Tasks reads;
/// a selection is saved first, so a failed save leaves the old folder current.
/// </summary>
internal sealed class RepositorySelection : IRepositorySelection
{
    private readonly IFileSystem _files;
    private readonly ITasks _tasks;
    private readonly string _settingFile;

    /// <param name="settingFile">The file that keeps the folder's path; created by the first selection.</param>
    public RepositorySelection(IFileSystem files, ITasks tasks, string settingFile)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingFile);
        _files = files;
        _tasks = tasks;
        _settingFile = settingFile;
    }

    public void Load()
    {
        var saved = ReadSetting() is { } bytes ? Encoding.UTF8.GetString(bytes).Trim() : "";
        if (saved.Length > 0 && !System.IO.Path.IsPathFullyQualified(saved))
        {
            throw new System.IO.InvalidDataException($"The saved repository folder '{saved}' is not a full path.");
        }

        _tasks.UseRepositoryFolder(saved.Length == 0 ? null : saved);
    }

    public void Select(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        // Read first: a setting that can't be read is never overwritten.
        var previous = ReadSetting();
        _files.WriteFile(_settingFile, Encoding.UTF8.GetBytes(folder));
        try
        {
            _tasks.UseRepositoryFolder(folder);
        }
        catch (Exception adoption)
        {
            Restore(previous, adoption);
            throw;
        }
    }

    // Puts the setting back exactly as it was; a failed restore reports both failures.
    private void Restore(byte[]? previous, Exception adoption)
    {
        try
        {
            if (previous is null)
            {
                _files.DeleteFile(_settingFile);
            }
            else
            {
                _files.WriteFile(_settingFile, previous);
            }
        }
        catch (Exception restore)
        {
            throw new AggregateException(adoption, restore);
        }
    }

    // The strict read: only a missing file means no saved selection.
    private byte[]? ReadSetting()
    {
        try
        {
            return _files.ReadFile(_settingFile);
        }
        catch (System.IO.FileNotFoundException)
        {
            return null;
        }
    }
}
