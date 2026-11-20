using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.MentionChip;

internal static class MentionInlines
{
    /// <summary>Replaces each mention's link with a <see cref="MentionInline"/> holding the same text, numbered in document order.</summary>
    public static void Wrap(MarkdownDocument document)
    {
        var number = 0;
        foreach (var link in document.Descendants<LinkInline>().Where(Mentions.IsMention).ToList())
        {
            link.ReplaceBy(new MentionInline(number++) { Span = link.Span }, copyChildren: true);
        }
    }
}
