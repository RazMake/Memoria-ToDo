using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

/// <summary>What a run of shown text looks like: its own styles and the ones it takes from the spans around it.</summary>
internal static class RunStyles
{
    public static bool IsBold(Run run) => run.FontWeight == FontWeights.Bold;

    public static bool IsItalic(Run run) => run.FontStyle == FontStyles.Italic;

    public static bool IsMonospaced(Run run)
    {
        var typeface = new Typeface(run.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphs))
        {
            return false;
        }

        return glyphs.AdvanceWidths[glyphs.CharacterToGlyphMap['i']] == glyphs.AdvanceWidths[glyphs.CharacterToGlyphMap['W']];
    }

    public static bool IsStruckThrough(Run run)
    {
        for (TextElement? element = run; element is not null; element = element.Parent as TextElement)
        {
            if (element is Inline { TextDecorations: { } decorations }
                && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the run sets a foreground of its own.</summary>
    public static bool HasOwnColor(Run run) =>
        run.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue;

    /// <summary>The run's foreground color; fails unless it is a solid color.</summary>
    public static Color ColorOf(Run run) => ((SolidColorBrush)run.Foreground).Color;
}
