using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.MentionChip;

internal static class MentionInlines
{
    /// <summary>Replaces each mention's link with a <see cref="MentionInline"/> holding the same text.</summary>
    public static void Wrap(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>().Where(Mentions.IsMention).ToList())
        {
            link.ReplaceBy(new MentionInline { Span = link.Span }, copyChildren: true);
        }
    }
}
