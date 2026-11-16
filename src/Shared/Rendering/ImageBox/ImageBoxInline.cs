using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.ImageBox;

/// <summary>The box around an image: its number in the Markdown, and its width in pixels, or 0 when it has none.</summary>
internal sealed class ImageBoxInline(int number, int width) : ContainerInline
{
    public int Number { get; } = number;
    public int Width { get; } = width;
}
