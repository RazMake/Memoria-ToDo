namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The rules for the text a task holds.</summary>
internal static class TaskText
{
    /// <summary>A title or tag: trimmed, and neither empty nor spread over lines.</summary>
    public static string SingleLine(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.AsSpan().ContainsAny('\r', '\n'))
            throw new ArgumentException("Must be one non-empty line.", paramName);
        return trimmed;
    }

    /// <summary>A description keeps the user's text, with every line break stored as <c>\n</c>.</summary>
    public static string LineFeeds(string description) => description.ReplaceLineEndings("\n");
}
