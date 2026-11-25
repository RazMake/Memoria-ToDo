using System.Text;

namespace Memoria.ToDo.Shared.Editing;

/// <summary>Makes a title that already holds Markdown safe as link text, keeping the escapes it has.</summary>
internal static class LinkLabelEscaping
{
    /// <summary>
    /// Puts a backslash before each "[" or "]" that has none (an even number of backslashes before it), and one more
    /// backslash at the end when the title ends with a lone backslash (an odd run), so it can't escape the "]" after it.
    /// </summary>
    public static string Of(string title)
    {
        var builder = new StringBuilder(title.Length + 2);
        var backslashes = 0;
        foreach (var c in title)
        {
            if (c is '[' or ']' && backslashes % 2 == 0)
            {
                builder.Append('\\');
            }

            builder.Append(c);
            backslashes = c == '\\' ? backslashes + 1 : 0;
        }

        if (backslashes % 2 == 1)
        {
            builder.Append('\\');
        }

        return builder.ToString();
    }
}
