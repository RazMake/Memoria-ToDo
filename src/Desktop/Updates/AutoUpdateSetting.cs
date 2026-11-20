using System.IO;
using System.Text;
using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>Auto update as <c>auto-update.txt</c> in the device folder keeps it: <c>on</c> or <c>off</c>, and off when there's no file.</summary>
internal sealed class AutoUpdateSetting(IFileSystem files, string deviceFolder, ILogger logger)
{
    private readonly string _path = Path.Combine(deviceFolder, "auto-update.txt");

    /// <summary>The saved value, or off with the reason when the file can't be read or holds something else. Logs that reason at Warning.</summary>
    public (bool On, string? Failure) Read()
    {
        try
        {
            if (!files.FileExists(_path))
            {
                return (false, null);
            }

            switch (Encoding.UTF8.GetString(files.ReadFile(_path)).Trim())
            {
                case "on":
                    return (true, null);
                case "off":
                    return (false, null);
                default:
                    logger.Warning("Auto update's file {Path} holds something other than on or off", _path);
                    return (false, "Auto update's file holds something other than on or off.");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.Warning(e, "Auto update's file {Path} can't be read", _path);
            return (false, $"Auto update's file can't be read: {e.Message}");
        }
    }

    /// <summary>Saves the value. Fails with the file system's exception and keeps the old file when it can't.</summary>
    public void Write(bool on) => files.WriteFile(_path, Encoding.UTF8.GetBytes(on ? "on" : "off"));
}
