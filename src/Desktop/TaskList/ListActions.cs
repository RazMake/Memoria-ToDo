using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Resolving, reopening, and moving tasks from the list. A failure the list knows is logged and reported with its reason and
/// answered with false; any other failure is not swallowed.
/// </summary>
internal sealed class ListActions(ITasks tasks, Action<string> report, ILogger logger)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Resolves the task, or reopens it when <paramref name="resolved"/> is false.</summary>
    public bool Resolve(string id, bool resolved) => Run(() => _ = resolved ? tasks.Resolve(id) : tasks.Reopen(id));

    public bool Move(Move move) => Run(() => tasks.Move(move.Id, move.After, move.Before));

    private bool Run(Action action) => ListFailures.Run(action, report, _logger);
}
