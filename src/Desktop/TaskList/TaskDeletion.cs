using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Deleting a task from the list: it hides at once, waits on the Undo bar, and is deleted for good when the bar ends.
/// A failure the list knows is reported with its reason.
/// </summary>
internal sealed class TaskDeletion(ITasks tasks, ListContent content, PendingUndo pending, Action<string> report, Action changed)
{
    public void Start(string taskId)
    {
        content.HiddenId = taskId;
        pending.Start("Task deleted", taskId, () => Commit(taskId), () =>
        {
            content.HiddenId = null;
            changed();
        });
        changed();
    }

    private void Commit(string id)
    {
        try
        {
            tasks.Delete(id);
        }
        catch (TaskNotFoundException)
        {
        }
        catch (Exception failure) when (ListFailures.MessageOf(failure) is { } why)
        {
            var removed = tasks.All.All(task => task.Id != id);
            report(removed ? "The task was deleted, but some of its images couldn't be removed: " + why : why);
        }
        finally
        {
            if (content.HiddenId == id)
            {
                content.HiddenId = null;
            }
        }

        changed();
    }
}
