using System.Globalization;
using System.IO;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// A scratch log folder of one test, removed afterwards, and how tests name and read the files in it. Dates are the
/// PC's local dates, as the log files' names use them.
/// </summary>
internal sealed class LogFolder : IDisposable
{
    public LogFolder() => Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "app-log", Guid.NewGuid().ToString("N"), "logs");

    /// <summary>The folder's path. It doesn't exist until the test or the log makes it.</summary>
    public string Path { get; }

    /// <summary>The file name for a day, with <paramref name="number"/> after the date when it isn't null: memoria-20261119.log, memoria-20261119_001.log.</summary>
    public static string FileName(DateTime day, int? number = null) =>
        $"memoria-{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}{(number is { } n ? $"_{n:D3}" : "")}.log";

    /// <summary>Makes a file of the name with some text in it, as an earlier run of the app left.</summary>
    public string Leave(string name, string text = "{\"@mt\":\"An old entry\"}\n")
    {
        Directory.CreateDirectory(Path);
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>The names of the files in the folder, sorted.</summary>
    public string[] Names() =>
        Directory.Exists(Path) ? [.. Directory.GetFiles(Path).Select(file => System.IO.Path.GetFileName(file)!).Order(StringComparer.Ordinal)] : [];

    /// <summary>Every line of every file, in file name order. Read it when the log is disposed, so no file is held.</summary>
    public string[] Lines() => [.. Names().SelectMany(name => File.ReadAllLines(System.IO.Path.Combine(Path, name)))];

    public void Dispose()
    {
        var root = System.IO.Path.GetDirectoryName(Path)!;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
