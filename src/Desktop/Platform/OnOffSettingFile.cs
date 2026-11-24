using System.IO;
using System.Text;
using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>One switch kept in a file on this device as <c>on</c> or <c>off</c>.</summary>
public sealed class OnOffSettingFile
{
    private readonly IFileSystem _files;
    private readonly string _path;
    private readonly bool _whenMissing;
    private readonly string _name;
    private readonly ILogger _logger;

    /// <summary>
    /// <paramref name="path"/> is the file's full path. <paramref name="whenMissing"/> is the value when there's no file.
    /// <paramref name="name"/> names the file in failures and log entries, such as "Auto update's file". Does no I/O.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> isn't fully qualified or <paramref name="name"/> is empty.</exception>
    public OnOffSettingFile(IFileSystem files, string path, bool whenMissing, string name, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(logger);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"The path isn't fully qualified: {path}", nameof(path));
        }

        ArgumentException.ThrowIfNullOrEmpty(name);
        _files = files;
        _path = path;
        _whenMissing = whenMissing;
        _name = name;
        _logger = logger;
    }

    /// <summary>
    /// The saved value, and a null failure; the missing-file value when there's no file. Off, with the failure saying why, when the file
    /// can't be read or holds something other than on or off; logs that at Warning. Other exceptions pass through.
    /// </summary>
    public (bool On, string? Failure) Read()
    {
        try
        {
            if (!_files.FileExists(_path))
            {
                return (_whenMissing, null);
            }

            switch (Encoding.UTF8.GetString(_files.ReadFile(_path)).Trim())
            {
                case "on":
                    return (true, null);
                case "off":
                    return (false, null);
                default:
                    _logger.Warning("{Name} {Path} holds something other than on or off, so it counts as off", _name, _path);
                    return (false, $"{_name} holds something other than on or off.");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(e, "{Name} {Path} can't be read, so it counts as off", _name, _path);
            return (false, $"{_name} can't be read: {e.Message}");
        }
    }

    /// <summary>Saves the value, replacing the whole file. Fails with the file system's exception and keeps the old file when it can't.</summary>
    public void Write(bool on) => _files.WriteFile(_path, Encoding.UTF8.GetBytes(on ? "on" : "off"));
}
