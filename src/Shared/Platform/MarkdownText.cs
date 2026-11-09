using System.Buffers;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>Text placed into Markdown.</summary>
public static class MarkdownText
{
    /// <summary><paramref name="text"/> with a backslash before each character Markdown could read as formatting, so it shows exactly as given.</summary>
    public static string Escape(string text)
    {
        int first = text.AsSpan().IndexOfAny(AsciiPunctuation);
        if (first < 0)
        {
            return text;
        }

        int escapes = 1;
        foreach (char c in text.AsSpan(first + 1))
        {
            if (AsciiPunctuation.Contains(c))
            {
                escapes++;
            }
        }

        return string.Create(text.Length + escapes, text, static (buffer, source) =>
        {
            int next = 0;
            foreach (char c in source)
            {
                if (AsciiPunctuation.Contains(c))
                {
                    buffer[next++] = '\\';
                }

                buffer[next++] = c;
            }
        });
    }

    private static readonly SearchValues<char> AsciiPunctuation = SearchValues.Create("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~");
}
