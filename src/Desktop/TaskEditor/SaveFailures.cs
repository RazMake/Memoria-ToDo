using System.IO;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The failures a save of Tasks is known to throw, and the one message each gets.</summary>
internal static class SaveFailures
{
    private const string NoRepositoryMessage =
        "There is no repository folder yet. Open Configure, choose a repository folder, then save again.";

    private const string IncompleteMessage =
        "The add was incomplete: the task was only partly added and couldn't be undone. Your text is kept; check the repository folder, then save again.";

    private const string TaskGoneMessage = "This task no longer exists. Copy your changes before leaving the editor.";

    /// <summary>The message for a known failure, or null when <paramref name="failure"/> isn't one the editor knows.</summary>
    public static string? MessageOf(Exception failure) => failure switch
    {
        NoRepositoryFolderException => NoRepositoryMessage,
        TaskNotFoundException => TaskGoneMessage,
        AggregateException => IncompleteMessage,
        IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or ImageTooLargeException =>
            $"The task wasn't saved: {failure.Message}",
        _ => null,
    };
}
