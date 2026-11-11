using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Memoria.ToDo.Shared.Rendering.ColorDirective;

namespace Memoria.ToDo.Shared.Rendering;

/// <summary>The Markdown rules of a description, read by both the styled spans and the page so they never disagree.</summary>
internal static class DescriptionMarkdown
{
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAlertBlocks()
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .DisableHtml()
        .UsePreciseSourceLocation()
        .UseColorDirective()
        .Build();
}
