namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The app's lifetime: the platform starts it and stops it. Shell implements it.</summary>
public interface IAppLifetime
{
    /// <summary>Starts the app on the UI thread. <paramref name="endApp"/> asks the platform to end the app from any thread, and returns without waiting for the app to end.</summary>
    void Start(Action endApp);

    /// <summary>Stops the app. The platform calls it once, when the app ends.</summary>
    void Stop();
}
