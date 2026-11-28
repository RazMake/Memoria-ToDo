using System.Globalization;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Memoria.ToDo.Shared.Rendering.MentionChip;

/// <summary>Shows a mention as a <c>span</c> the page's style block gives its background; nothing in it can be clicked.</summary>
internal sealed class MentionInlineRenderer : HtmlObjectRenderer<MentionInline>
{
    protected override void Write(HtmlRenderer renderer, MentionInline mention)
    {
        if (!renderer.EnableHtmlForInline)
        {
            renderer.WriteChildren(mention);
            return;
        }

        renderer.Write("<span class=\"mention\" data-mention=\"").Write(mention.Number.ToString(CultureInfo.InvariantCulture)).Write("\">");
        renderer.WriteChildren(mention);
        renderer.Write("</span>");
    }
}
