namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The words a window shows when Tasks throws several failures at once, so the change may be only partly done.</summary>
public static class SaveFailureText
{
    /// <summary>
    /// "The change may be only partly saved: " followed by the message of the first failure inside <paramref name="failure"/>,
    /// or by its own message when it holds none. Throws <see cref="ArgumentNullException"/> for a null <paramref name="failure"/>.
    /// </summary>
    public static string Incomplete(AggregateException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var first = failure.InnerExceptions.Count > 0 ? failure.InnerExceptions[0] : failure;
        return $"The change may be only partly saved: {first.Message}";
    }
}
