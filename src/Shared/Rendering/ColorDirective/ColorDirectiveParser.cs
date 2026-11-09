using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.ColorDirective;

/// <summary>
/// Reads <c>:name[text]</c> for a palette name. A colon opens a <see cref="ColorInline"/>; a <c>]</c> closes the nearest
/// open color unless an open link bracket is nearer, which keeps brackets inside balanced. A color never closed stays open
/// and the reader of the tree shows it as text.
/// </summary>
internal sealed class ColorDirectiveParser : InlineParser
{
    public ColorDirectiveParser()
    {
        OpeningCharacters = [':', ']'];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice) =>
        slice.CurrentChar == ':' ? Open(processor, ref slice) : Close(processor, ref slice);

    private static bool Open(InlineProcessor processor, ref StringSlice slice)
    {
        if (slice.Start > 0 && char.IsLetterOrDigit(slice.Text[slice.Start - 1]))
        {
            return false;
        }

        var nameStart = slice.Start + 1;
        var nameEnd = nameStart;
        while (nameEnd <= slice.End && slice.Text[nameEnd] is >= 'a' and <= 'z')
        {
            nameEnd++;
        }

        if (nameEnd > slice.End || slice.Text[nameEnd] != '[')
        {
            return false;
        }

        var name = PaletteName(slice.Text.AsSpan(nameStart, nameEnd - nameStart));
        if (name is null)
        {
            return false;
        }

        var start = processor.GetSourcePosition(slice.Start, out var line, out var column);
        slice.Start = nameEnd + 1;
        processor.Inline = new ColorInline(name)
        {
            Span = new SourceSpan(start, processor.GetSourcePosition(nameEnd)),
            Line = line,
            Column = column,
        };
        return true;
    }

    private static bool Close(InlineProcessor processor, ref StringSlice slice)
    {
        for (var inline = processor.Inline; inline is not null; inline = inline.Parent)
        {
            if (inline is LinkDelimiterInline)
            {
                return false;
            }

            if (inline is ColorInline { IsClosed: false } color)
            {
                color.Span.End = processor.GetSourcePosition(slice.Start);
                slice.SkipChar();
                color.IsClosed = true;
                processor.PostProcessInlines(0, color, null, false);
                processor.Inline = color;
                return true;
            }
        }

        return false;
    }

    private static string? PaletteName(ReadOnlySpan<char> name)
    {
        foreach (var color in Palette.Colors)
        {
            if (name.SequenceEqual(color.Name))
            {
                return color.Name;
            }
        }

        return null;
    }
}
