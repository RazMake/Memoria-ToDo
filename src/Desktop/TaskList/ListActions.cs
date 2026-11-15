using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Resolving, reopening, and moving tasks from the list. A failure the list knows is reported with its reason and
/// answered with false; any other failure is not swallowed.
/// </summary>
internal sealed class ListActions(ITasks tasks, Action<string> report)
{
    /// <summary>Resolves the task, or reopens it when <paramref name="resolved"/> is false.</summary>
    public bool Resolve(string id, bool resolved) => Run(() => _ = resolved ? tasks.Resolve(id) : tasks.Reopen(id));

    public bool Move(Move move) => Run(() => tasks.Move(move.Id, move.After, move.Before));

    private bool Run(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception failure) when (ListFailures.MessageOf(failure) is { } why)
        {
            report(why);
            return false;
        }
    }
}
