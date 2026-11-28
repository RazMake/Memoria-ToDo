using System.IO;
using Memoria.ToDo.Desktop.Platform;
using Serilog;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The app's log, written to files in a folder. A new file starts each day and whenever a file reaches 1 MB; at most
/// 30 files are kept, and none from a day more than 14 days ago. Each line is one entry in Serilog's compact JSON
/// format, with secrets replaced. Entries go through a <see cref="LogPipeline"/>, so a slow or failing disk never
/// holds up or breaks the app.
/// </summary>
internal sealed class AppLog : IDisposable
{
    private const long FileSizeLimit = 1024 * 1024;
    private const int FileCountLimit = 30;
    private const string FileName = "memoria-.log";
    private static readonly TimeSpan FileAgeLimit = TimeSpan.FromDays(14);

    private readonly LogPipeline _pipeline;

    private AppLog(LogPipeline pipeline) => _pipeline = pipeline;

    /// <summary>The pattern that matches every file the log writes in its folder, such as memoria-20261119.log and memoria-20261119_001.log.</summary>
    public static string FilesPattern { get; } = Path.GetFileNameWithoutExtension(FileName) + "*" + Path.GetExtension(FileName);

    /// <summary>The logs folder of the app's folder under %LocalAppData%, apart from Velopack's install folder.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName.Value, "logs");

    /// <summary>Why entries can't be written now, or null while they can.</summary>
    public string? WriteProblem => _pipeline.WriteProblem;

    /// <summary>
    /// Makes the log for the folder. A folder that can't be made or used doesn't throw: the entries are dropped and
    /// counted, and <see cref="WriteProblem"/> says why.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="folder"/> is null, empty, or blank.</exception>
    public static AppLog Create(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ITextFormatter format = new RedactingFormatter(new CompactJsonFormatter());
        return new AppLog(LogPipeline.CreateWriting(
            files => files.File(
                format,
                Path.Combine(folder, FileName),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: FileSizeLimit,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: FileCountLimit,
                retainedFileTimeLimit: FileAgeLimit),
            TimeProvider.System));
    }

    /// <summary>The logger of the module with this name, as the module map spells it.</summary>
    public ILogger ForModule(string module) => _pipeline.ForModule(module);

    /// <summary>Writes what is queued, waiting at most 2 seconds.</summary>
    public void Dispose() => _pipeline.Dispose();
}
