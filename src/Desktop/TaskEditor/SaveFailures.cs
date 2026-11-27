using System.IO;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Platform;
using Serilog;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The failures a save of Tasks is known to throw, and the one message each gets.</summary>
internal static class SaveFailures
{
    private const string NoRepositoryMessage =
        "There is no repository folder yet. Open Configure, choose a repository folder, then save again.";

    private const string TaskGoneMessage = "This task no longer exists. Copy your changes before leaving the editor.";

    /// <summary>The message for a known failure, or null when <paramref name="failure"/> isn't one the editor knows.</summary>
    public static string? MessageOf(Exception failure) => failure switch
    {
        NoRepositoryFolderException => NoRepositoryMessage,
        TaskNotFoundException => TaskGoneMessage,
        AggregateException aggregate => SaveFailureText.Incomplete(aggregate),
        IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or ImageTooLargeException =>
            $"The task wasn't saved: {failure.Message}",
        _ => null,
    };

    /// <summary>
    /// Calls <paramref name="save"/> with the value and clears the message, unless the save was kept and a notice failed
    /// after it: then the shared message <see cref="StoreFailures.Run"/> reports stays shown. Returns false, showing the
    /// failure's message, when <paramref name="save"/> threw a known failure, which it also logs at Error; other failures propagate.
    /// </summary>
    public static bool Try<T>(Action<T> save, T value, Action<string> showMessage, ILogger logger)
    {
        var shown = false;
        if (!StoreFailures.Run(() => save(value), MessageOf, message => { shown = true; showMessage(message); }, logger))
        {
            return false;
        }

        if (!shown)
        {
            showMessage("");
        }

        return true;
    }
}
