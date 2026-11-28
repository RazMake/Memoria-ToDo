namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>Starts and stops the local API.</summary>
public interface ILocalApiLifetime
{
    /// <summary>
    /// Starts listening in the background and returns at once. Logs a failure to start; never throws it.
    /// Repeated calls do nothing.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops listening, ends open streams, and waits at most 5 seconds for running calls.
    /// Works when Start never ran; repeated calls join the same stop.
    /// </summary>
    Task StopAsync();
}
