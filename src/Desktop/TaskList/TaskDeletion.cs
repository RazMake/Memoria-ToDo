using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Deleting a task from the list: it hides at once, waits on the Undo bar, and is deleted for good when the bar ends.
/// A failure the list knows is logged and reported with its reason; a delete saved before a notice failed is reported with the shared message.
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
            StoreFailures.Run(
                () =>
                {
                    try
                    {
                        tasks.Delete(id);
                    }
                    catch (TaskNotFoundException)
                    {
                    }
                },
                failure => ListFailures.MessageOf(failure) is { } why ? WithImagesNote(id, failure, why) : null,
                report,
                _logger);
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

    // A delete that is done but couldn't remove the task's images says so. Several failures at once say what may be unsaved; the images
    // prefix would claim a delete they can't promise. A notice that failed after the delete gets the shared message from Run, with no images named.
    private string WithImagesNote(string id, Exception failure, string why) =>
        tasks.All.All(task => task.Id != id) && failure is not AggregateException
            ? "The task was deleted, but some of its images couldn't be removed: " + why
            : why;
}
