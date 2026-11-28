using System.IO;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.KeepAwake;

public static class KeepAwake
{
    /// <summary>
    /// Creates Keep awake. Reads nothing and asks Windows nothing until <see cref="IKeepAwakeLifetime.Start"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="deviceFolder"/> isn't fully qualified.</exception>
    public static IKeepAwake Create(IDisplayRequest display, ISessionLock session, IFileSystem files, string deviceFolder, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(deviceFolder);
        ArgumentNullException.ThrowIfNull(logger);
        if (!Path.IsPathFullyQualified(deviceFolder))
        {
            throw new ArgumentException($"The device folder isn't a full path: {deviceFolder}", nameof(deviceFolder));
        }

        var setting = new OnOffSettingFile(files, Path.Combine(deviceFolder, "prevent-screen-saver.txt"), whenMissing: false, "Prevent screen saver's file", logger);
        return new KeepAwakeService(display, session, setting, logger);
    }
}
