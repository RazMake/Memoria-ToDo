using System.IO;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The failures a Tasks call from the list is known to throw, and the message each gets.</summary>
internal static class ListFailures
{
    /// <summary>The message for a known failure, or null when <paramref name="failure"/> isn't one the list knows.</summary>
    public static string? MessageOf(Exception failure) => failure switch
    {
        TaskNotFoundException => "This task no longer exists.",
        NoRepositoryFolderException => "There is no repository folder yet. Open Configure and choose one.",
        IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException => failure.Message,
        _ => null,
    };
}
