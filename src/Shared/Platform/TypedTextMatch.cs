namespace Memoria.ToDo.Shared.Platform;

/// <summary>How typed text matches a name, such as a snippet's or a tag's.</summary>
public static class TypedTextMatch
{
    /// <summary>
    /// True when <paramref name="name"/>, from the start of any of its words, starts with <paramref name="typed"/>, ignoring case.
    /// A word starts at the beginning of the name or after a space. Empty typed text matches every name.
    /// </summary>
    public static bool Matches(string name, string typed)
    {
        ReadOnlySpan<char> rest = name;
        while (true)
        {
            if (rest.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int space = rest.IndexOf(' ');
            if (space < 0)
            {
                return false;
            }

            rest = rest[(space + 1)..];
        }
    }
}
