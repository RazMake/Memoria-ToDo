using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The Markdown that shows a kept image in a description.</summary>
internal static class ImageMarkdown
{
    public static string Of(string altText, string taskId, string fileName)
    {
        return $"![{MarkdownLabel.Escape(altText)}]({RepositoryLayout.ImageLink(taskId, fileName)})";
    }
}
