namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The rules for the text a task holds.</summary>
internal static class TaskText
{
    /// <summary>A title: trimmed, and neither empty nor spread over lines.</summary>
    public static string SingleLine(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        var trimmed = value.Trim();
        if (!IsSingleLine(trimmed))
            throw new ArgumentException("Must be one non-empty line.", paramName);
        return trimmed;
    }

    /// <summary>Whether the text is neither blank nor spread over lines.</summary>
    public static bool IsSingleLine(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.AsSpan().ContainsAny('\r', '\n');

    /// <summary>A description keeps the user's text, with every line break stored as <c>\n</c>.</summary>
    public static string LineFeeds(string description) => description.ReplaceLineEndings("\n");
}
