using System.Globalization;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.ImageBox;

/// <summary>
/// Shows an image the page loads in a <c>span</c> that carries its number and width; an image shown as a link, or inside
/// alt text, shows without one. The page's style block makes the box resizable.
/// </summary>
internal sealed class ImageBoxRenderer : HtmlObjectRenderer<ImageBoxInline>
{
    protected override void Write(HtmlRenderer renderer, ImageBoxInline box)
    {
        if (!renderer.EnableHtmlForInline || box.FirstChild is not LinkInline { IsImage: true })
        {
            renderer.WriteChildren(box);
            return;
        }

        renderer.Write("<span data-image=\"").Write(box.Number.ToString(CultureInfo.InvariantCulture)).Write('"');
        if (box.Width > 0)
        {
            renderer.Write(" style=\"width:").Write(box.Width.ToString(CultureInfo.InvariantCulture)).Write("px\"");
        }

        renderer.Write('>');
        renderer.WriteChildren(box);
        renderer.Write("</span>");
    }
}
