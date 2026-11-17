using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Moving a group among the groups, through Tasks' tag order. A failure the list knows is reported with its reason and
/// answered with false, and the group stays where it was; any other failure is not swallowed.
/// </summary>
internal sealed class GroupActions(ITagOrder tagOrder, Action<string> report)
{
    public bool Move(GroupMove move) => ListFailures.Run(() => tagOrder.Move(move.Tag, move.Previous, move.Next), report);
}
