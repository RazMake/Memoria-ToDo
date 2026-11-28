using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.MentionChip;

/// <summary>A mention on the page: the text of its link, without the link's address or template.</summary>
internal sealed class MentionInline(int number) : ContainerInline
{
    /// <summary>The mention's place among the page's mentions, from 0, as <see cref="Mentions.In"/> lists them.</summary>
    public int Number { get; } = number;
}
