namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>The user resized an image in a description: its position among the description's images, and its new width in pixels.</summary>
public sealed record ImageResize(int Image, int Width);
