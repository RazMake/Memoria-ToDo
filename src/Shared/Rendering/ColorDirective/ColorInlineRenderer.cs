using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Memoria.ToDo.Shared.Rendering.ColorDirective;

/// <summary>Shows a closed color as a <c>span</c> in the theme's palette value; a color never closed shows as the text typed.</summary>
internal sealed class ColorInlineRenderer(bool dark) : HtmlObjectRenderer<ColorInline>
{
    protected override void Write(HtmlRenderer renderer, ColorInline color)
    {
        if (!color.IsClosed)
        {
            renderer.WriteEscape(":" + color.Name + "[");
            renderer.WriteChildren(color);
            return;
        }

        var palette = Palette.Colors.Single(c => c.Name == color.Name);
        renderer.Write("<span style=\"color:").Write(dark ? palette.Dark : palette.Light).Write("\">");
        renderer.WriteChildren(color);
        renderer.Write("</span>");
    }
}
