namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>What Configure uses.</summary>
public interface IMcpServerStatus
{
    /// <summary>The state now.</summary>
    McpServerState State { get; }

    /// <summary>Raised on the UI thread after <see cref="State"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Turns the server on or off, saves it on this device, and starts or stops listening at once once
    /// <see cref="IMcpServerLifetime.Start"/> has run. Fails with the file system's exception, keeping the previous value, when the
    /// switch can't be saved.
    /// </summary>
    void SetOn(bool on);

    /// <summary>What each assistant needs to connect, in the order Configure shows them.</summary>
    IReadOnlyList<McpConnection> Connections { get; }
}
