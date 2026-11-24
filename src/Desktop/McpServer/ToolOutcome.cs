using System.Runtime.CompilerServices;

namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>
/// Tells the code around a tool call whether the tool's own runner got to run it, and so logged its outcome. A call the SDK
/// refuses while binding its input never gets that far.
/// </summary>
internal static class ToolOutcome
{
    private static readonly AsyncLocal<StrongBox<bool>?> Current = new();

    /// <summary>Starts watching the call that is about to run on this flow; the box turns true once the runner is reached.</summary>
    public static StrongBox<bool> Watch() => Current.Value = new StrongBox<bool>();

    /// <summary>Said by the runner when it starts a call.</summary>
    public static void Reached()
    {
        if (Current.Value is { } box)
        {
            box.Value = true;
        }
    }
}
