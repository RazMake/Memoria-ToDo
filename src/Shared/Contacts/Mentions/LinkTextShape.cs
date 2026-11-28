namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Checks that a template's own text can sit inside a Markdown link's text without breaking the link.</summary>
internal static class LinkTextShape
{
    /// <summary>
    /// What's wrong with the template's text, or null when it fits: brackets pair, no <c>](</c> or <c>![</c>, no trailing lone backslash,
    /// no lone backslash right before a placeholder, no line break.
    /// </summary>
    public static string? ProblemWith(string template)
    {
        if (template.AsSpan().ContainsAny('\r', '\n'))
            return "The template has a line break.";

        var depth = 0;
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '\\')
            {
                if (++i == template.Length)
                    return "The template ends with a '\\' that escapes nothing.";
                if (StartsPlaceholder(template, i))
                    return "The template has a '\\' right before a placeholder, which would cancel the escape on the value's first character.";
            }
            else if (c == '[')
            {
                if (i > 0 && template[i - 1] == '!' && !IsEscaped(template, i - 1))
                    return "The template has '![', which starts an image.";
                depth++;
            }
            else if (c == ']')
            {
                if (--depth < 0)
                    return "A ']' has no '['.";
                if (i + 1 < template.Length && template[i + 1] == '(')
                    return "The template has '](', which starts a link address.";
            }
        }

        return depth > 0 ? "A '[' has no ']'." : null;
    }

    // "{{" writes a literal brace, so only a single "{" starts a placeholder.
    private static bool StartsPlaceholder(string text, int index) =>
        text[index] == '{' && (index + 1 == text.Length || text[index + 1] != '{');

    private static bool IsEscaped(string text, int index)
    {
        var backslashes = 0;
        while (index - 1 - backslashes >= 0 && text[index - 1 - backslashes] == '\\')
            backslashes++;
        return backslashes % 2 == 1;
    }
}
