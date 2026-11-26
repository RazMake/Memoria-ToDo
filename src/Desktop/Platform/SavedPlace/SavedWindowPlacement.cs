using System.IO;
using System.Text.Json;
using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>One window's size, place, and maximized state, kept in one file on this device between runs.</summary>
public sealed class SavedWindowPlacement
{
    private const int Version = 1;

    private readonly IFileSystem _files;
    private readonly string _path;
    private readonly string _name;
    private readonly ILogger _logger;

    /// <summary>
    /// Reads nothing yet. <paramref name="path"/> is the file's full path. <paramref name="name"/> is how the log calls it,
    /// such as "Task View's place". <paramref name="logger"/> is the caller's own.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> isn't fully qualified or <paramref name="name"/> is empty.</exception>
    public SavedWindowPlacement(IFileSystem files, string path, string name, ILogger logger)
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
        _name = name;
        _logger = logger;
    }

    /// <summary>
    /// The saved place; null with no Warning when there's no file. Also null, with a Warning naming the file and the reason,
    /// when the file can't be read, needs a newer version, lacks a field, or holds a width or height of zero or less or a
    /// number that doesn't fit in 32 bits.
    /// </summary>
    internal SavedPlace? Read()
    {
        try
        {
            if (!_files.FileExists(_path))
            {
                _logger.Debug("{Name} {Path} has no saved place yet", _name, _path);
                return null;
            }

            using var document = JsonFile.Parse(_files.ReadFile(_path));
            var root = document.RootElement;
            JsonFile.RequireVersion(root, Version);
            var place = new SavedPlace(Int(root, "left"), Int(root, "top"), Int(root, "width"), Int(root, "height"), Bool(root, "maximized"));
            if (place.Width <= 0 || place.Height <= 0)
            {
                throw new InvalidDataException("The width or height is zero or less.");
            }

            return place;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger.Warning(e, "{Name} {Path} can't be read, so the window has no saved place: {Reason}", _name, _path, e.Message);
            return null;
        }
    }

    /// <summary>Saves the place, replacing the file. When that fails, logs a Warning with the exception and the old file stays.</summary>
    internal void Write(SavedPlace place)
    {
        var bytes = JsonFile.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteNumber("left", place.Left);
            writer.WriteNumber("top", place.Top);
            writer.WriteNumber("width", place.Width);
            writer.WriteNumber("height", place.Height);
            writer.WriteBoolean("maximized", place.Maximized);
            writer.WriteEndObject();
        });

        try
        {
            _files.WriteFile(_path, bytes);
            _logger.Debug("Saved {Name} {Path}: {Place}", _name, _path, place);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(e, "{Name} {Path} can't be written, so it keeps what it held", _name, _path);
        }
    }

    private static int Int(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new InvalidDataException($"'{field}' is missing, not a whole number, or out of range.");

    private static bool Bool(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new InvalidDataException($"'{field}' is missing or not true or false.");
}
