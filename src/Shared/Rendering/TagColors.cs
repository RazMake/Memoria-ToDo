using System.Globalization;
using System.Text;

namespace Memoria.ToDo.Shared.Rendering;

/// <summary>A tag color's value in one theme, such as #fca5a5, and its text color: #000000 or #ffffff.</summary>
public sealed record TagFill(string Background, string Text);

/// <summary>A tag color's name, such as red-1, and its fill in each theme.</summary>
public sealed record TagColor(string Name, TagFill Light, TagFill Dark);

public static class TagColors
{
    private const string Black = "#000000";
    private const string White = "#ffffff";

    // Each hue's three shades, palest first, as light then dark values.
    private static readonly (string Hue, string[] Light, string[] Dark)[] Hues =
    [
        ("red", ["#fca5a5", "#ef4444", "#b91c1c"], ["#f87171", "#dc2626", "#7f1d1d"]),
        ("orange", ["#fdba74", "#f97316", "#c2410c"], ["#fb923c", "#ea580c", "#9a3412"]),
        ("yellow", ["#fde047", "#eab308", "#a16207"], ["#facc15", "#ca8a04", "#854d0e"]),
        ("lime", ["#bef264", "#84cc16", "#4d7c0f"], ["#a3e635", "#65a30d", "#3f6212"]),
        ("green", ["#86efac", "#22c55e", "#15803d"], ["#4ade80", "#16a34a", "#166534"]),
        ("teal", ["#5eead4", "#14b8a6", "#0f766e"], ["#2dd4bf", "#0d9488", "#115e59"]),
        ("blue", ["#93c5fd", "#3b82f6", "#1d4ed8"], ["#60a5fa", "#2563eb", "#1e40af"]),
        ("purple", ["#c4b5fd", "#8b5cf6", "#6d28d9"], ["#a78bfa", "#7c3aed", "#5b21b6"]),
        ("pink", ["#f9a8d4", "#ec4899", "#be185d"], ["#f472b6", "#db2777", "#9d174d"]),
        ("gray", ["#d6d3d1", "#78716c", "#44403c"], ["#a8a29e", "#78716c", "#44403c"]),
    ];

    private static readonly TagColor[] Colors = Build();

    private static readonly Dictionary<string, TagColor> ByName = Colors.ToDictionary(c => c.Name, StringComparer.Ordinal);

    /// <summary>The 30 tag colors, in the table's order: each hue's three shades, palest first.</summary>
    public static IReadOnlyList<TagColor> All { get; } = Array.AsReadOnly(Colors);

    /// <summary>The color with exactly this name, or null when no tag color has it.</summary>
    public static TagColor? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ByName.GetValueOrDefault(name);
    }

    /// <summary>A tag's default color, from its text with case ignored; the same tag always gets the same color.</summary>
    public static TagColor DefaultFor(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(tag.ToUpperInvariant()))
        {
            hash = (hash ^ b) * 16777619u;
        }

        return Colors[(int)(hash % (uint)Colors.Length)];
    }

    private static TagColor[] Build()
    {
        var colors = new List<TagColor>(Hues.Length * 3);
        foreach (var (hue, light, dark) in Hues)
        {
            for (var shade = 0; shade < light.Length; shade++)
            {
                colors.Add(new TagColor($"{hue}-{shade + 1}", Fill(light[shade]), Fill(dark[shade])));
            }
        }

        return [.. colors];
    }

    private static TagFill Fill(string background)
    {
        var luminance = Luminance(background);
        var blackContrast = (luminance + 0.05) / 0.05;
        var whiteContrast = 1.05 / (luminance + 0.05);
        return new TagFill(background, blackContrast > whiteContrast ? Black : White);
    }

    // WCAG 2.2 relative luminance of a "#rrggbb" value.
    private static double Luminance(string hex) =>
        0.2126 * Linear(hex, 1) + 0.7152 * Linear(hex, 3) + 0.0722 * Linear(hex, 5);

    private static double Linear(string hex, int from)
    {
        var c = int.Parse(hex.AsSpan(from, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
