using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// The steps an operation has done, so a failure can undo them, newest first. Every undo runs even when an earlier one failed.
/// When no undo fails, the operation's own failure is rethrown as it is; otherwise an <see cref="AggregateException"/>
/// holds it first, then each undo's failure in the order they happened.
/// </summary>
internal sealed class UndoSteps(string failedMessage, ILogger logger)
{
    private readonly Stack<Action> steps = new();

    /// <summary>Remembers how to undo a step that has just succeeded.</summary>
    public void Push(Action undo) => steps.Push(undo);

    /// <summary>Runs the work. When it throws, undoes every step pushed so far, then throws as the class says.</summary>
    public void Run(Action work)
    {
        try
        {
            work();
        }
        catch (Exception failure)
        {
            UndoAfter(failure);
            throw;
        }
    }

    private void UndoAfter(Exception failure)
    {
        var failures = new FailureList();
        failures.Add(failure);
        var undoFailed = false;
        foreach (var step in steps)
        {
            failures.Attempt(() =>
            {
                try
                {
                    step();
                }
                catch
                {
                    undoFailed = true;
                    throw;
                }
            });
        }

        logger.Debug("Undid the steps of a failed operation: {Steps} to undo, undo {UndoOutcome}", steps.Count, undoFailed ? "failed" : "succeeded");
        if (!undoFailed)
            return;
        try
        {
            failures.ThrowIfAny();
        }
        catch (AggregateException all)
        {
            throw new AggregateException(failedMessage, all.InnerExceptions);
        }
    }
}
