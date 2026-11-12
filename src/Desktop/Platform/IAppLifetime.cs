namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The app's lifetime: the platform starts it and stops it. Shell implements it.</summary>
public interface IAppLifetime
{
    /// <summary>Starts the app on the UI thread. <paramref name="endApp"/> asks the platform to end the app from any thread, and returns without waiting for the app to end.</summary>
    void Start(Action endApp);

    /// <summary>
    /// Begins shutdown on the UI thread and asynchronously joins all owned cleanup.
    /// Repeated calls join the same shutdown. Cancellation stops only this caller's wait.
    /// The platform keeps its dispatcher running until normal shutdown completes.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
