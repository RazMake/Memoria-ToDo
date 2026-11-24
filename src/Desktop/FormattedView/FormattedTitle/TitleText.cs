using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>What a title shows of its Markdown: the spans that aren't markers or link addresses, and their text.</summary>
internal static class TitleText
{
    /// <summary>The spans of the title that show, in order.</summary>
    public static IReadOnlyList<StyledSpan> ShownSpans(string markdown) =>
        StyledSpans.Of(markdown, MarkdownField.Title).Where(span => !span.Styles.HasFlag(SpanStyle.Marker)).ToList();

    /// <summary>The text the title shows, which is also the name assistive tools read for it.</summary>
    public static string Shown(string markdown) => TextOf(markdown, ShownSpans(markdown));

    /// <summary>The text of <paramref name="spans"/>, taken from <paramref name="markdown"/>.</summary>
    public static string TextOf(string markdown, IReadOnlyList<StyledSpan> spans) =>
        string.Concat(spans.Select(span => markdown.AsSpan(span.Start, span.Length).ToString()));
}
