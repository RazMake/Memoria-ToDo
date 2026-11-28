using Markdig;

namespace Memoria.ToDo.Shared.Rendering.ColorDirective;

internal static class ColorDirectiveExtension
{
    /// <summary>Adds the <c>:name[text]</c> reader to the pipeline.</summary>
    public static MarkdownPipelineBuilder UseColorDirective(this MarkdownPipelineBuilder builder)
    {
        builder.InlineParsers.Insert(0, new ColorDirectiveParser());
        return builder;
    }
}
