using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Memoria.ToDo.Shared.Rendering.ColorDirective;

namespace Memoria.ToDo.Shared.Rendering.Painting;

/// <summary>
/// Turns a parsed Markdown tree into styled spans in one walk. The walk visits the text in order, so spans come out sorted;
/// text no node claims (line breaks, block prefixes) is filled in as plain spans, so the spans cover the whole text; and
/// neighbours with the same look are merged.
/// </summary>
internal sealed class SpanPainter
{
    private readonly record struct Look(SpanStyle Styles, string? LinkAddress, string? Color, int HeadingLevel);

    private readonly string _text;
    private readonly List<StyledSpan> _spans = [];
    private Look _filler;      // the look of text between nodes: that of the node being walked
    private Look _pending;
    private int _pendingStart = -1;
    private int _end;          // the end of everything placed so far

    private SpanPainter(string text) => _text = text;

    public static List<StyledSpan> Paint(string text, MarkdownDocument document)
    {
        var painter = new SpanPainter(text);
        foreach (var block in document)
        {
            painter.VisitBlock(block);
        }

        painter.Place(painter._end, text.Length, painter._filler);
        painter.Flush();
        return painter._spans;
    }

    private delegate void BlockHandler(SpanPainter painter, Block block);

    private delegate void InlineHandler(SpanPainter painter, Inline inline, in Look look);

    // Handlers by node type. A node uses the handler of its nearest registered base type, so a new kind of node is one new line here.
    private static readonly Dictionary<Type, BlockHandler> BlockHandlers = new()
    {
        [typeof(HeadingBlock)] = static (p, b) =>
        {
            var heading = (HeadingBlock)b;
            p.VisitFramed(heading.Span, heading.Inline, new Look(SpanStyle.None, null, null, heading.Level));
        },
        [typeof(LeafBlock)] = static (p, b) =>
        {
            if (((LeafBlock)b).Inline is { } content)
            {
                p.VisitChildren(content, p._filler);
            }
        },
        [typeof(ContainerBlock)] = static (p, b) =>
        {
            foreach (var child in (ContainerBlock)b)
            {
                p.VisitBlock(child);
            }
        },
    };

    private static readonly Dictionary<Type, InlineHandler> InlineHandlers = new()
    {
        [typeof(ColorInline)] = static (SpanPainter p, Inline i, in Look look) =>
        {
            var color = (ColorInline)i;
            if (color.IsClosed)
            {
                p.VisitFramed(color.Span, color, look with { Color = color.Name });
            }
            else
            {
                p.VisitUnclosed(color, look);
            }
        },
        [typeof(DelimiterInline)] = static (SpanPainter p, Inline i, in Look look) => p.VisitUnclosed((ContainerInline)i, look),
        [typeof(EmphasisInline)] = static (SpanPainter p, Inline i, in Look look) =>
        {
            var emphasis = (EmphasisInline)i;
            p.VisitFramed(emphasis.Span, emphasis, look with { Styles = look.Styles | StyleOf(emphasis) });
        },
        [typeof(LinkInline)] = static (SpanPainter p, Inline i, in Look look) =>
        {
            var link = (LinkInline)i;
            p.VisitFramed(link.Span, link, look with { LinkAddress = link.Url });
        },
        [typeof(ContainerInline)] = static (SpanPainter p, Inline i, in Look look) => p.VisitChildren((ContainerInline)i, look),
        [typeof(AutolinkInline)] = static (SpanPainter p, Inline i, in Look look) =>
        {
            var auto = (AutolinkInline)i;
            var marker = Marked(look);
            p.Place(auto.Span.Start, auto.Span.Start + 1, marker);
            p.Place(auto.Span.Start + 1, auto.Span.End, look with { LinkAddress = auto.Url });
            p.Place(auto.Span.End, auto.Span.End + 1, marker);
        },
        [typeof(CodeInline)] = static (SpanPainter p, Inline i, in Look look) => p.VisitCode((CodeInline)i, look),
        [typeof(LiteralInline)] = static (SpanPainter p, Inline i, in Look look) =>
        {
            var literal = (LiteralInline)i;
            if (literal.IsFirstCharacterEscaped)
            {
                p.Place(literal.Span.Start, literal.Span.Start + 1, Marked(look));
                p.Place(literal.Span.Start + 1, literal.Span.End + 1, look);
            }
            else
            {
                p.PlaceSpan(literal.Span, look);
            }
        },
        [typeof(LineBreakInline)] = static (SpanPainter p, Inline i, in Look look) => { },
    };

    private void VisitBlock(Block block)
    {
        if (HandlerFor(BlockHandlers, block.GetType()) is { } handler)
        {
            handler(this, block);
        }
    }

    private void VisitChildren(ContainerInline container, in Look look)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            VisitInline(child, look);
        }
    }

    private void VisitInline(Inline inline, in Look look)
    {
        if (HandlerFor(InlineHandlers, inline.GetType()) is { } handler)
        {
            handler(this, inline, look);
        }
        else
        {
            PlaceSpan(inline.Span, look);
        }
    }

    /// <summary>Opened, never closed: the opening characters are text and the content carries on as the parent's.</summary>
    private void VisitUnclosed(ContainerInline inline, in Look look)
    {
        PlaceSpan(inline.Span, look);
        VisitChildren(inline, look);
    }

    private static THandler? HandlerFor<THandler>(Dictionary<Type, THandler> handlers, Type type)
        where THandler : Delegate
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (handlers.TryGetValue(t, out var handler))
            {
                return handler;
            }
        }

        return null;
    }
    /// <summary>Walks a node whose own characters (brackets, stars, hashes) sit before its first child and after its last, and marks them.</summary>
    private void VisitFramed(SourceSpan span, ContainerInline? content, in Look look)
    {
        var first = FirstWithSpan(content);
        if (first is null)
        {
            Place(span.Start, span.End + 1, Marked(look));
            return;
        }

        Place(span.Start, first.Span.Start, Marked(look));
        var outer = _filler;
        _filler = look;
        VisitChildren(content!, look);
        _filler = outer;
        Place(LastWithSpan(content!)!.Span.End + 1, span.End + 1, Marked(look));
    }

    private void VisitCode(CodeInline code, in Look look)
    {
        var start = code.Span.Start;
        var end = code.Span.End + 1;
        var marker = Marked(look);
        Place(start, start + code.DelimiterCount, marker);
        Place(start + code.DelimiterCount, end - code.DelimiterCount, look with { Styles = look.Styles | SpanStyle.Code });
        Place(end - code.DelimiterCount, end, marker);
    }

    private static Inline? FirstWithSpan(ContainerInline? container)
    {
        for (var child = container?.FirstChild; child is not null; child = child.NextSibling)
        {
            if (!child.Span.IsEmpty)
            {
                return child;
            }
        }

        return null;
    }

    private static Inline? LastWithSpan(ContainerInline container)
    {
        for (var child = container.LastChild; child is not null; child = child.PreviousSibling)
        {
            if (!child.Span.IsEmpty)
            {
                return child;
            }
        }

        return null;
    }

    private static SpanStyle StyleOf(EmphasisInline emphasis) =>
        emphasis.DelimiterChar == '~' ? SpanStyle.Strikethrough
        : emphasis.DelimiterCount >= 2 ? SpanStyle.Bold
        : SpanStyle.Italic;

    private static Look Marked(in Look look) => look with { Styles = look.Styles | SpanStyle.Marker };

    private void PlaceSpan(SourceSpan span, in Look look)
    {
        if (!span.IsEmpty)
        {
            Place(span.Start, span.End + 1, look);
        }
    }

    /// <summary>Adds the text from <paramref name="start"/> to <paramref name="end"/>, first filling any gap before it, and ignoring what's already placed.</summary>
    private void Place(int start, int end, in Look look)
    {
        start = Math.Max(start, _end);
        end = Math.Min(end, _text.Length);
        if (end <= start)
        {
            return;
        }

        if (start > _end)
        {
            Append(_end, start, _filler);
        }

        Append(start, end, look);
    }

    private void Append(int start, int end, in Look look)
    {
        if (_pendingStart >= 0 && look == _pending)
        {
            _end = end;
            return;
        }

        Flush();
        _pendingStart = start;
        _pending = look;
        _end = end;
    }

    private void Flush()
    {
        if (_pendingStart >= 0)
        {
            _spans.Add(new StyledSpan(_pendingStart, _end - _pendingStart, _pending.Styles, _pending.LinkAddress, _pending.Color, _pending.HeadingLevel));
            _pendingStart = -1;
        }
    }
}
