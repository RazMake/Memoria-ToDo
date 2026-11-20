using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>A mention of a title, and where its text sits in <see cref="TitleText.Shown"/>.</summary>
internal sealed record ShownMention(Mention Mention, int ShownStart, int ShownLength);

/// <summary>Which mention a place in a title's shown text is on.</summary>
internal static class TitleMentions
{
    /// <summary>
    /// The mention whose text holds <paramref name="shownOffset"/>, an offset into <see cref="TitleText.Shown"/>, or null when
    /// no mention does. A mention's text ends before its last offset plus one: the end is exclusive.
    /// </summary>
    public static ShownMention? At(string markdown, int shownOffset)
    {
        var mentions = Mentions.In(markdown, title: true);
        if (mentions.Count == 0 || shownOffset < 0)
        {
            return null;
        }

        var spans = TitleText.ShownSpans(markdown);
        foreach (var mention in mentions)
        {
            var start = 0;
            var length = 0;
            foreach (var span in spans)
            {
                if (span.Start < mention.Start)
                {
                    start += span.Length;
                }
                else if (span.Start < mention.Start + mention.Length)
                {
                    length += span.Length;
                }
            }

            if (shownOffset >= start && shownOffset < start + length)
            {
                return new ShownMention(mention, start, length);
            }
        }

        return null;
    }
}
