using System.Runtime.ExceptionServices;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>Collects the failures of steps that must all run, then throws them together.</summary>
public sealed class FailureList
{
    private List<Exception>? failures;

    /// <summary>Runs the step, and keeps its exception instead of throwing it.</summary>
    public void Attempt(Action step)
    {
        ArgumentNullException.ThrowIfNull(step);
        try
        {
            step();
        }
        catch (Exception e)
        {
            Add(e);
        }
    }

    /// <summary>Keeps a failure caught elsewhere. Throws <see cref="ArgumentNullException"/> for null.</summary>
    public void Add(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        (failures ??= []).Add(failure);
    }

    /// <summary>
    /// Does nothing when no failure was kept. Throws the one failure as it is, keeping its stack trace,
    /// or an <see cref="AggregateException"/> with every failure, in the order they were kept.
    /// </summary>
    public void ThrowIfAny()
    {
        if (failures is null)
            return;
        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException("More than one step failed.", failures);
    }
}
