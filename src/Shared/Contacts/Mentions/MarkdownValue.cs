using System.Text;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Makes a value show as written inside Markdown link text, by putting a backslash before every ASCII punctuation or symbol character.</summary>
internal static class MarkdownValue
{
    public static string Escape(string value)
    {
        if (!value.Any(NeedsEscape))
            return value;

        var escaped = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if (NeedsEscape(c))
                escaped.Append('\\');
            escaped.Append(c);
        }

        return escaped.ToString();
    }

    private static bool NeedsEscape(char c) => char.IsAscii(c) && (char.IsPunctuation(c) || char.IsSymbol(c));
}
