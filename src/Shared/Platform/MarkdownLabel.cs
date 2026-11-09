using System.Text;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>Makes text safe to put between the brackets of a Markdown link or image, so it can't end the label early.</summary>
public static class MarkdownLabel
{
    /// <summary>The text with a backslash before each "[", "]", and "\". Every other character, such as the "*" of Markdown emphasis, is kept.</summary>
    public static string Escape(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (label.AsSpan().IndexOfAny('[', ']', '\\') < 0)
            return label;

        var escaped = new StringBuilder(label.Length + 4);
        foreach (var c in label)
        {
            if (c is '[' or ']' or '\\')
                escaped.Append('\\');
            escaped.Append(c);
        }

        return escaped.ToString();
    }
}
