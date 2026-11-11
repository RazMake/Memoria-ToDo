using System.Net;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Memoria.ToDo.Shared.Rendering.ColorDirective;

namespace Memoria.ToDo.Shared.Rendering;

public static class DescriptionPage
{
    // Text colors for the theme; the page sets no background so the window shows through.
    private const string LightText = "#1f1f1f";
    private const string DarkText = "#f3f3f3";

    /// <summary>
    /// A full HTML page of the description for the web view, in the light or dark theme.
    /// It holds no script and no raw HTML from the task, and loads images only from cloneAddress:
    /// a relative image path, such as images/&lt;task-id&gt;/&lt;image-id&gt;.png, loads from it.
    /// Throws ArgumentException when cloneAddress isn't an absolute http or https address ending in "/".
    /// </summary>
    public static string Of(string markdown, bool dark, Uri cloneAddress)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(cloneAddress);
        if (!cloneAddress.IsAbsoluteUri
            || !BrowserCheck.MayOpen(cloneAddress.AbsoluteUri)
            || !cloneAddress.AbsoluteUri.EndsWith('/'))
        {
            throw new ArgumentException("The clone address must be an absolute http or https address ending in \"/\".", nameof(cloneAddress));
        }

        var document = Markdown.Parse(markdown, DescriptionMarkdown.Pipeline);
        CloneImages.Restrict(document, cloneAddress);

        var writer = new StringWriter();
        writer.Write("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\">\n<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src ");
        writer.Write(WebUtility.HtmlEncode(cloneAddress.AbsoluteUri));
        writer.Write("; style-src 'unsafe-inline'\">\n</head><body style=\"color:");
        writer.Write(dark ? DarkText : LightText);
        writer.Write("\">\n");

        var renderer = new HtmlRenderer(writer);
        DescriptionMarkdown.Pipeline.Setup(renderer);
        renderer.ObjectRenderers.Insert(0, new ColorInlineRenderer(dark));
        renderer.ObjectRenderers.FindExact<LineBreakInlineRenderer>()!.RenderAsHardlineBreak = true;
        renderer.Render(document);

        writer.Write("</body></html>\n");
        return writer.ToString();
    }
}
