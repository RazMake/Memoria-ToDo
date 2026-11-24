using Memoria.ToDo.Desktop.MCPServer;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The AI assistants decision behind the card: whether the switch is on, the status line, the lines to copy, and why a switch or
/// a copy failed. It shows what the MCP server's status says and passes the user's switch and copies on.
/// </summary>
internal sealed class AiAssistantsSetting
{
    private readonly IMcpServerStatus _server;
    private readonly ISetupActions _actions;
    private readonly ILogger _logger;

    /// <param name="logger">Where a failed switch or copy is logged.</param>
    public AiAssistantsSetting(IMcpServerStatus server, ISetupActions actions, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(logger);
        _server = server;
        _actions = actions;
        _logger = logger;
    }

    /// <summary>Whether assistants are let in: the server's switch now.</summary>
    public bool IsOn => _server.State.IsOn;

    /// <summary>The status line: the server's failure when it has one, otherwise running, starting, or off.</summary>
    public string Status => _server.State switch
    {
        { Failure: { } failure } => failure,
        { IsOn: false } => "Off. AI assistants can't reach Memoria-ToDo.",
        { Listening: false } => "Starting…",
        _ => "Running. AI assistants on this PC can use Memoria-ToDo.",
    };

    /// <summary>Whether <see cref="Status"/> is the server's failure, which shows in the error color.</summary>
    public bool StatusIsFailure => _server.State.Failure is not null;

    /// <summary>Why the last switch or copy failed, or null when it succeeded.</summary>
    public string? Problem { get; private set; }

    /// <summary>What each assistant needs to connect, in the server's order.</summary>
    public IReadOnlyList<McpConnection> Connections => _server.Connections;

    /// <summary>Switches the server. A failure leaves <see cref="IsOn"/> as it was and sets <see cref="Problem"/>.</summary>
    public void Set(bool on)
    {
        try
        {
            _server.SetOn(on);
            Problem = null;
        }
        catch (Exception error)
        {
            _logger.Error(error, "Switching AI assistants {State} failed", on ? "on" : "off");
            Problem = error.Message;
        }
    }

    /// <summary>Copies the assistant's lines. A failure sets <see cref="Problem"/>.</summary>
    public void Copy(McpConnection connection)
    {
        try
        {
            _actions.CopyText(connection.Text);
            Problem = null;
        }
        catch (Exception error)
        {
            _logger.Error(error, "Copying the lines for {Assistant} failed", connection.Assistant);
            Problem = error.Message;
        }
    }
}
