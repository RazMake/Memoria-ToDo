namespace Memoria.ToDo.Shared.Rendering;

/// <summary>A palette color's name and its values, such as #d1242f, for each theme.</summary>
public sealed record PaletteColor(string Name, string Light, string Dark);

public static class Palette
{
    /// <summary>The palette, in the table's order.</summary>
    public static IReadOnlyList<PaletteColor> Colors { get; } = Array.AsReadOnly(
    [
        new PaletteColor("red", "#d1242f", "#ff7b72"),
        new PaletteColor("orange", "#bc4c00", "#f0883e"),
        new PaletteColor("green", "#1a7f37", "#3fb950"),
        new PaletteColor("blue", "#0969da", "#4493f8"),
        new PaletteColor("purple", "#8250df", "#ab7df8"),
        new PaletteColor("gray", "#59636e", "#9198a1"),
    ]);
}
