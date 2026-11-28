using System.Text;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// A store saved a change, and then one or more of its notices, or its local save report, failed.
/// The change stays saved.
/// </summary>
public class NoticeFailedException : Exception
{
    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> for null failures, and <see cref="ArgumentException"/> when failures is empty
    /// or holds null. <see cref="Exception.InnerException"/> is the first failure.
    /// </summary>
    public NoticeFailedException(IReadOnlyList<Exception> failures)
        : base(MessageOf(failures), failures[0])
    {
        InnerExceptions = failures.ToArray();
    }

    /// <summary>Every failure, in the order they happened. Never empty.</summary>
    public IReadOnlyList<Exception> InnerExceptions { get; }

    /// <summary>The exception's own text, then every failure's full text, stack trace included, in order.</summary>
    public override string ToString()
    {
        var text = new StringBuilder().Append(GetType()).Append(": ").Append(Message);
        if (StackTrace is { } stackTrace)
            text.AppendLine().Append(stackTrace);
        for (var i = 0; i < InnerExceptions.Count; i++)
            text.AppendLine().Append("---> (Failure #").Append(i + 1).Append(") ").Append(InnerExceptions[i]);
        return text.ToString();
    }

    private static string MessageOf(IReadOnlyList<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
            throw new ArgumentException("Name at least one failure.", nameof(failures));
        if (failures.Any(failure => failure is null))
            throw new ArgumentException("A failure can't be null.", nameof(failures));
        return "A notice failed after the change was saved: " + failures[0].Message;
    }
}

/// <summary><see cref="NoticeFailedException"/> with what the change saved.</summary>
public sealed class NoticeFailedException<T> : NoticeFailedException where T : class
{
    /// <summary>Throws as the base constructor does.</summary>
    public NoticeFailedException(T? saved, IReadOnlyList<Exception> failures)
        : base(failures)
    {
        Saved = saved;
    }

    /// <summary>The item as saved, or null when the change saves no item.</summary>
    public T? Saved { get; }
}
