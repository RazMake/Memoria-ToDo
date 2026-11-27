using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// A change of one task made from the list with no Edit: it saves at once through Tasks' save of an edit, with the task as
/// shown, then waits on the Undo bar, which saves the opposite change. A failure the list knows is logged and reported with its
/// reason and nothing is saved, except after several failures at once: the change then counts as saved, with its bar, when
/// Tasks holds it. A change saved before a notice failed counts as saved too, with the shared message reported.
/// </summary>
internal sealed class UndoableSave(ITasks tasks, PendingUndo pending, EditCleanup cleanup, Action<string> report, ILogger logger)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    /// <summary>
    /// Saves <paramref name="shown"/> as <paramref name="change"/> makes it, and puts <paramref name="label"/> on the Undo bar,
    /// where Undo saves what <paramref name="opposite"/> makes of the saved task. Returns the saved task, or null when it
    /// couldn't be saved.
    /// </summary>
    public TaskItem? Run(TaskItem shown, string label, Func<TaskItem, TaskDraft> change, Func<TaskItem, TaskDraft> opposite)
    {
        // The earlier bar finishes first, even when this save fails; a discarded edit's bar deletes its images, so the save finds none to move.
        cleanup.FinishBar();
        if (!TrySave(shown, change, out var saved))
        {
            return null;
        }

        pending.Start(label, saved.Id, () => { }, () => TrySave(saved, opposite, out _));
        return saved;
    }

    private bool TrySave(TaskItem started, Func<TaskItem, TaskDraft> draft, out TaskItem saved)
    {
        var wanted = draft(started);
        TaskItem? kept = null;
        var done = ListFailures.Run(
            () =>
            {
                try
                {
                    kept = tasks.SaveEdit(started, wanted);
                }
                catch (NoticeFailedException<TaskItem> notice)
                {
                    kept = notice.Saved;
                    throw;
                }
                catch (AggregateException)
                {
                    // Several failures at once may leave the change saved: it counts as saved when Tasks now holds the new tags.
                    kept = HeldNow(started.Id, wanted.Tags);
                    throw;
                }
            },
            report,
            _logger);

        if (kept is not null)
        {
            if (!done)
            {
                _logger.Debug("The change was saved although Tasks threw several failures; it stays undoable");
            }

            saved = kept;
            return true;
        }

        saved = started;
        return done;
    }

    private TaskItem? HeldNow(string id, IReadOnlyList<string> tags) =>
        tasks.All.FirstOrDefault(task => task.Id == id && task.Tags.Select(TagName.Normalize).SequenceEqual(tags.Select(TagName.Normalize)));
}
