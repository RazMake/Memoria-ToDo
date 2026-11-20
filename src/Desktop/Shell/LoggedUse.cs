using Serilog;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Logs each use of a tray item or shortcut, at Information, before it runs.</summary>
internal static class LoggedUse
{
    /// <summary>An action that logs <paramref name="use"/>, then runs <paramref name="action"/>.</summary>
    public static Action Of(ILogger logger, string use, Action action) => () =>
    {
        logger.Information("{Use}", use);
        action();
    };
}
