using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Parsers;
using Memoria.ToDo.Shared.Rendering.ColorDirective;
using Memoria.ToDo.Shared.Rendering.Painting;

namespace Memoria.ToDo.Shared.Rendering;

public enum MarkdownField { Title, Description }

[Flags]
public enum SpanStyle { None = 0, Bold = 1, Italic = 2, Strikethrough = 4, Code = 8, Marker = 16 }

/// <summary>One stretch of the text. LinkAddress and Color are null, and HeadingLevel is 0, when they don't apply.</summary>
public sealed record StyledSpan(int Start, int Length, SpanStyle Styles, string? LinkAddress, string? Color, int HeadingLevel);

public static class StyledSpans
{
    // A title is inline Markdown only: the only block is the paragraph.
    private static readonly MarkdownPipeline TitlePipeline = InlineOnly(new MarkdownPipelineBuilder())
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .DisableHtml()
        .UsePreciseSourceLocation()
        .UseColorDirective()
        .Build();

    /// <summary>The spans of the field's Markdown, in order, never overlapping.</summary>
    public static IReadOnlyList<StyledSpan> Of(string markdown, MarkdownField field)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var pipeline = field == MarkdownField.Title ? TitlePipeline : DescriptionMarkdown.Pipeline;
        return SpanPainter.Paint(markdown, Markdown.Parse(markdown, pipeline));
    }

    private static MarkdownPipelineBuilder InlineOnly(MarkdownPipelineBuilder builder)
    {
        builder.BlockParsers.Clear();
        builder.BlockParsers.Add(new ParagraphBlockParser());
        return builder;
    }
}
