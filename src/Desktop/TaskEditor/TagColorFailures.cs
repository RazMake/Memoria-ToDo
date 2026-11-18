using System.IO;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The failures keeping a tag's color is known to throw, and the one message each gets.</summary>
internal static class TagColorFailures
{
    private const string NoRepositoryMessage =
        "There is no repository folder yet. Open Configure, choose a repository folder, then add the tag again.";

    /// <summary>The message for a known failure, or null when <paramref name="failure"/> isn't one the tag control knows.</summary>
    public static string? MessageOf(Exception failure) => failure switch
    {
        NoRepositoryFolderException => NoRepositoryMessage,
        IOException or UnauthorizedAccessException or InvalidDataException =>
            $"The tag wasn't added, because its color couldn't be kept: {failure.Message}",
        _ => null,
    };
}
