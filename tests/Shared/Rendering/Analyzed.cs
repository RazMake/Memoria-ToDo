using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>A Markdown text and its spans, with a way to ask which spans lie over a piece of the text.</summary>
internal sealed class Analyzed
{
    private readonly string _markdown;

    public Analyzed(string markdown, MarkdownField field)
    {
        _markdown = markdown;
        All = StyledSpans.Of(markdown, field);
    }

    public IReadOnlyList<StyledSpan> All { get; }

    /// <summary>The spans that overlap the first occurrence of <paramref name="text"/> at or after <paramref name="startAt"/>. Fails when the text isn't there or no span covers it.</summary>
    public IReadOnlyList<StyledSpan> Over(string text, int startAt = 0)
    {
        var start = _markdown.IndexOf(text, startAt, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The test's Markdown has no '{text}' to look at.");
        var end = start + text.Length;
        var over = All.Where(s => s.Start < end && s.Start + s.Length > start).ToList();
        Assert.NotEmpty(over);
        return over;
    }

    public static Analyzed OfDescription(string markdown) => new(markdown, MarkdownField.Description);

    public static Analyzed OfTitle(string markdown) => new(markdown, MarkdownField.Title);
}
