namespace Memoria.ToDo.Desktop.Updates;

/// <summary>What Shell uses.</summary>
public interface IUpdateLifetime
{
    /// <summary>
    /// Reads Auto update and starts the checks: one now, then each night at 3:00 local time.
    /// Checks nothing when the app isn't installed. Called once.
    /// </summary>
    void Start(IAppExit exit);

    /// <summary>
    /// Cancels a check or a download under way and waits for it. When an update ended the app, starts
    /// Velopack's updater as its last step. Repeated calls join the same stop.
    /// </summary>
    Task StopAsync();
}
