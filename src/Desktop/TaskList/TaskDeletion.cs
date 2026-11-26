using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Deleting a task from the list: it hides at once, waits on the Undo bar, and is deleted for good when the bar ends.
/// A failure the list knows is logged and reported with its reason.
/// </summary>
internal sealed class TaskDeletion(ITasks tasks, ListContent content, PendingUndo pending, Action<string> report, Action changed, ILogger logger)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _logger.Error(failure, "A task couldn't be deleted: {Reason}", why);
            var removed = tasks.All.All(task => task.Id != id);
            // Several failures at once say what may be unsaved; the images prefix would claim a delete they can't promise.
            report(removed && failure is not AggregateException ? "The task was deleted, but some of its images couldn't be removed: " + why : why);
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
