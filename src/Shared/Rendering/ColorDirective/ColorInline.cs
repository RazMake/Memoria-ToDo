using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.ColorDirective;

/// <summary>The inlines of <c>:name[…]</c>. Its span runs from the colon to the closing bracket; it is closed only when that bracket was found.</summary>
internal sealed class ColorInline(string name) : ContainerInline
{
    public string Name { get; } = name;
}
