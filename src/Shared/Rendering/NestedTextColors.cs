using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Memoria.ToDo.Shared.Rendering.ColorDirective;
using Memoria.ToDo.Shared.Rendering.MentionChip;

namespace Memoria.ToDo.Shared.Rendering;

/// <summary>
/// Makes a link, italic text, or inline code inside other styled text show in the color <see cref="TextLook.ColorOf"/> gives its stretch.
/// The page's style block colors each of them alone, and the nearest colored element wins in a browser,
/// so each one whose color differs from that gets its own color.
/// </summary>
internal static class NestedTextColors
{
    public static void Apply(MarkdownDocument document, TextLook look)
    {
        foreach (var inline in document.Descendants<Inline>())
        {
            if (!IsColoredAlone(inline, out var alone))
            {
                continue;
            }

            var color = look.ColorOf(StretchOf(inline, alone));
            if (color != look.ColorOf(alone))
            {
                // No color of its own means the surrounding text's, such as a mention's.
                inline.GetAttributes().AddProperty("style", $"color:{color ?? "inherit"}");
            }
        }
    }

    // The elements the page's style block gives a color of their own, as the stretch of text that element alone makes.
    private static bool IsColoredAlone(Inline inline, out StyledSpan alone)
    {
        alone = inline switch
        {
            LinkInline { IsImage: false } link => new StyledSpan(0, 0, SpanStyle.None, link.Url, null, 0),
            AutolinkInline auto => new StyledSpan(0, 0, SpanStyle.None, auto.Url, null, 0),
            CodeInline => new StyledSpan(0, 0, SpanStyle.Code, null, null, 0),
            EmphasisInline { DelimiterChar: '*' or '_', DelimiterCount: 1 } => new StyledSpan(0, 0, SpanStyle.Italic, null, null, 0),
            _ => null!,
        };

        return alone is not null;
    }

    // The element's own style plus every style around it, the nearest link and palette color counting.
    private static StyledSpan StretchOf(Inline inline, StyledSpan alone)
    {
        var styles = alone.Styles;
        var link = alone.LinkAddress;
        string? color = null;
        ContainerInline? top = null;
        for (var parent = inline.Parent; parent is not null; parent = parent.Parent)
        {
            top = parent;
            switch (parent)
            {
                case ColorInline { IsClosed: true } palette:
                    color ??= palette.Name;
                    break;
                case LinkInline { IsImage: false } outer:
                    link ??= outer.Url;
                    break;
                case MentionInline:
                    styles |= SpanStyle.Mention;
                    break;
                case EmphasisInline { DelimiterChar: '*' or '_', DelimiterCount: 1 }:
                    styles |= SpanStyle.Italic;
                    break;
            }
        }

        var heading = top?.ParentBlock is HeadingBlock block ? block.Level : 0;
        return new StyledSpan(0, 0, styles, link, color, heading);
    }
}
