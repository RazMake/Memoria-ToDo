using System.Reflection;
using System.Runtime.ExceptionServices;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// A <see cref="ITasks"/> whose tag suggestions the test decides, so the Task editor's tests don't depend on how Tasks
/// matches text. Every other member goes to the real Tasks it wraps. Shared test setup.
/// </summary>
public class SuggestingTasks : DispatchProxy
{
    private ITasks _inner = null!;

    /// <summary>What <see cref="ITasks.SuggestTags"/> answers: the tags to suggest for the typed text and the task's tags.</summary>
    public Func<string, IReadOnlyList<string>, IReadOnlyList<string>> Suggest { get; set; } = (_, _) => [];

    /// <summary>Every call to <see cref="ITasks.SuggestTags"/>, in order.</summary>
    public List<(string Typed, IReadOnlyList<string> Tags)> Calls { get; } = [];

    public static (ITasks Tasks, SuggestingTasks Suggesting) Over(ITasks inner)
    {
        var tasks = Create<ITasks, SuggestingTasks>();
        var suggesting = (SuggestingTasks)(object)tasks;
        suggesting._inner = inner;
        return (tasks, suggesting);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name == nameof(ITasks.SuggestTags))
        {
            var typed = (string)args![0]!;
            var tags = (IReadOnlyList<string>)args[1]!;
            Calls.Add((typed, tags));
            return Suggest(typed, tags);
        }

        try
        {
            return targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException failure)
        {
            ExceptionDispatchInfo.Capture(failure.InnerException!).Throw();
            throw;
        }
    }
}
