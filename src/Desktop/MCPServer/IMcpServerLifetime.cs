namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>What Shell uses.</summary>
public interface IMcpServerLifetime
{
    /// <summary>
    /// Reads the switch and, when it's on, starts listening. Called once, on the UI thread, which is where the calls to
    /// Updates and to Start with Windows, and <see cref="IMcpServerStatus.Changed"/>, run afterwards.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops listening, refuses calls that arrive, cancels the calls in progress that can be cancelled and waits for all of them,
    /// then ends every connection. Repeated calls join the same stop. Never blocks the calling thread.
    /// </summary>
    Task StopAsync();
}
