namespace Memoria.ToDo.Shared.Rendering;

/// <summary>The look of headings, links, code, and italic text in one theme.</summary>
public sealed class TextLook
{
    private readonly bool _dark;

    internal TextLook(
        bool dark, string heading1, string heading2, string heading3, string link, string code, string italic,
        double codeBackgroundOpacity)
    {
        _dark = dark;
        Heading1 = heading1;
        Heading2 = heading2;
        Heading3 = heading3;
        Link = link;
        Code = code;
        Italic = italic;
        CodeBackgroundOpacity = codeBackgroundOpacity;
    }

    public string Heading1 { get; }
    public string Heading2 { get; }
    public string Heading3 { get; }
    public string Link { get; }
    public string Code { get; }
    public string Italic { get; }

    /// <summary>The gray behind code, and how opaque it is: from 0 to 1.</summary>
    public string CodeBackground => "#808080";
    public double CodeBackgroundOpacity { get; }

    /// <summary>A heading's color, or null for levels 4 to 6. Throws ArgumentOutOfRangeException for a level outside 1 to 6.</summary>
    public string? HeadingColor(int level) => level switch
    {
        1 => Heading1,
        2 => Heading2,
        3 => Heading3,
        >= 4 and <= 6 => null,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "A heading level is from 1 to 6."),
    };

    /// <summary>The color the span shows in, in this theme, or null for the text color.</summary>
    public string? ColorOf(StyledSpan span)
    {
        if (span.Color is { } name)
        {
            foreach (var color in Palette.Colors)
            {
                if (color.Name == name)
                {
                    return _dark ? color.Dark : color.Light;
                }
            }
        }

        if (span.LinkAddress is not null)
        {
            return Link;
        }

        if ((span.Styles & SpanStyle.Code) != 0)
        {
            return Code;
        }

        if ((span.Styles & SpanStyle.Italic) != 0)
        {
            return Italic;
        }

        return span.HeadingLevel is >= 1 and <= 6 ? HeadingColor(span.HeadingLevel) : null;
    }
}

public static class TextLooks
{
    private static readonly double[] HeadingScales = [1, 1.6, 1.4, 1.25, 1.15, 1.1, 1.05];

    public static TextLook Light { get; } =
        new(dark: false, "#0f548c", "#0e7a75", "#6b3fa0", "#0969da", "#9a3c00", "#4f5b93", 0.15);

    public static TextLook Dark { get; } =
        new(dark: true, "#6cb8f6", "#4fd1c5", "#c4a7f5", "#4493f8", "#f0a868", "#b4bdf5", 0.25);

    /// <summary>Light or Dark.</summary>
    public static TextLook Of(bool dark) => dark ? Dark : Light;

    /// <summary>How many times the text size a heading of this level shows; 1 for level 0. Throws ArgumentOutOfRangeException outside 0 to 6.</summary>
    public static double HeadingScale(int level) =>
        (uint)level < (uint)HeadingScales.Length
            ? HeadingScales[level]
            : throw new ArgumentOutOfRangeException(nameof(level), level, "A heading level is from 0 to 6.");

    /// <summary>The monospaced font code shows in.</summary>
    public static string CodeFont => "Consolas";
}
