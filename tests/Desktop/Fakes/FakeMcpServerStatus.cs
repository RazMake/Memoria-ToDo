using Memoria.ToDo.Desktop.McpServer;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IMcpServerStatus"/>, for the tests of the windows that show the MCP server. A test sets
/// the state and the connections, publishes a change with <see cref="Publish"/>, which raises <see cref="Changed"/> on the
/// calling thread, and reads the calls the window made. It starts on, listening, with no failure and no connections.
/// </summary>
public sealed class FakeMcpServerStatus : IMcpServerStatus
{
    private readonly List<bool> setOnCalls = [];

    /// <inheritdoc />
    public McpServerState State { get; set; } = new(true, true, null);

    /// <inheritdoc />
    public IReadOnlyList<McpConnection> Connections { get; set; } = [];

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>When set, <see cref="SetOn"/> throws it and the state stays as it was.</summary>
    public Exception? SetOnFailure { get; set; }

    /// <summary>The values <see cref="SetOn"/> was called with, in order, including those that failed.</summary>
    public IReadOnlyList<bool> SetOnCalls => setOnCalls;

    /// <summary>Sets <see cref="State"/>, then raises <see cref="Changed"/>.</summary>
    public void Publish(McpServerState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void SetOn(bool on)
    {
        setOnCalls.Add(on);
        if (SetOnFailure is not null)
        {
            throw SetOnFailure;
        }

        Publish(new McpServerState(on, on, null));
    }
}
