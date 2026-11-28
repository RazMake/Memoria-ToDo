using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering;

/// <summary>Keeps images only when they lie inside the clone; any other image becomes a link with its alt text.</summary>
internal static class CloneImages
{
    public static void Restrict(MarkdownDocument document, Uri clone)
    {
        foreach (var link in document.Descendants<LinkInline>().Where(l => l.IsImage).ToList())
        {
            if (link.Url is not null
                && Uri.TryCreate(clone, link.Url, out var resolved)
                && resolved.AbsoluteUri.StartsWith(clone.AbsoluteUri, StringComparison.Ordinal))
            {
                link.Url = resolved.AbsoluteUri;
            }
            else
            {
                link.IsImage = false;
            }
        }
    }
}
