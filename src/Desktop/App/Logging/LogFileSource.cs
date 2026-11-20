using System.Globalization;
using System.IO;
using System.Text;
using Memoria.ToDo.Desktop.Logs;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Reads the log files <see cref="AppLog"/> writes, for the Logs window, with Serilog's compact-format reader. It opens each
/// file so the app can keep writing the one it's reading, and reads the files side by side. A file that can't be read is left
/// out and logged at Warning; a broken line is skipped and counted.
/// </summary>
internal sealed class LogFileSource : ILogSource
{
    private const string UnknownComponent = "Unknown";

    private readonly string _folder;
    private readonly Func<string?> _writeProblem;
    private readonly ILogger _logger;

    public LogFileSource(string folder, Func<string?> writeProblem, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(writeProblem);
        ArgumentNullException.ThrowIfNull(logger);
        _folder = folder;
        _writeProblem = writeProblem;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>A folder that doesn't exist yet holds no entries. Throws when the folder's path exists but can't be listed.</remarks>
    public Task<LogRead> ReadAsync(CancellationToken cancellationToken) =>
        Task.Run(
            async () =>
            {
                var files = await Task.WhenAll(Files().Select(file => Task.Run(() => ReadFile(file, cancellationToken), cancellationToken)));
                var entries = new List<LogEntry>(files.Sum(file => file.Entries.Count));
                foreach (var file in files)
                {
                    entries.AddRange(file.Entries);
                }

                return new LogRead(entries, files.Sum(file => file.UnreadableLines), _writeProblem());
            },
            cancellationToken);

    private string[] Files()
    {
        // A file where the folder should be isn't "no folder yet": Directory would report it missing, so say it's unusable.
        if (File.Exists(_folder))
        {
            throw new IOException($"The log folder '{_folder}' is a file.");
        }

        try
        {
            return Directory.GetFiles(_folder, AppLog.FilesPattern);
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private (List<LogEntry> Entries, int UnreadableLines) ReadFile(string path, CancellationToken cancellationToken)
    {
        var entries = new List<LogEntry>();
        var unreadable = 0;
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.SequentialScan,
            };
            using var reader = new LogEventReader(new StreamReader(new FileStream(path, options), Encoding.UTF8));
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!reader.TryRead(out var logEvent))
                    {
                        break;
                    }

                    entries.Add(Entry(logEvent));
                }
                // Whatever the reader can't parse in this one line: broken JSON, a bad time, level, or value.
                catch (Exception bad) when (bad is Newtonsoft.Json.JsonReaderException or FormatException or ArgumentException
                    or InvalidCastException or InvalidDataException)
                {
                    unreadable++;
                }
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(failure, "The log file {File} could not be read and is left out", Path.GetFileName(path));
            return ([], 0);
        }

        return (entries, unreadable);
    }

    private static LogEntry Entry(LogEvent logEvent) =>
        new(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var context) && context is ScalarValue { Value: string name }
                ? string.Intern(name)
                : UnknownComponent,
            logEvent.RenderMessage(CultureInfo.InvariantCulture),
            logEvent.Exception?.ToString());
}
