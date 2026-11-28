namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The rules for a list of tags: each tag normalized, a repeat dropped, and at most <see cref="TagName.MaxPerTask"/> in a new list.</summary>
internal static class TagList
{
    /// <summary>The tags normalized, each repeat dropped and the first place kept. For a list read from storage, where nothing is refused.</summary>
    public static string[] Read(IEnumerable<string> tags) =>
        [.. tags.Select(TagName.Normalize).Distinct(StringComparer.Ordinal)];

    /// <summary>The tags as <see cref="Read"/> gives them. Throws <see cref="ArgumentException"/> for a tag that's empty once normalized.</summary>
    public static string[] Normalized(IEnumerable<string> tags, string paramName)
    {
        ArgumentNullException.ThrowIfNull(tags, paramName);
        var normalized = Read(tags);
        return normalized.Contains("") ? throw new ArgumentException("A tag can't be empty.", paramName) : normalized;
    }

    /// <summary>The tags as <see cref="Normalized"/> gives them. Throws <see cref="ArgumentException"/> for more than <see cref="TagName.MaxPerTask"/>.</summary>
    public static string[] NewList(IEnumerable<string> tags, string paramName)
    {
        var normalized = Normalized(tags, paramName);
        RequireWithinLimit(normalized, paramName);
        return normalized;
    }

    /// <summary>Throws <see cref="ArgumentException"/> when a new list holds more than <see cref="TagName.MaxPerTask"/> tags.</summary>
    public static void RequireWithinLimit(IReadOnlyCollection<string> tags, string paramName)
    {
        if (tags.Count > TagName.MaxPerTask)
            throw new ArgumentException($"A task has at most {TagName.MaxPerTask} tags.", paramName);
    }

    /// <summary><paramref name="tags"/> normalized with the typed tag added at the end; the normalized list when it's already there. Throws <see cref="ArgumentException"/> for an empty tag, or when a new tag would pass <see cref="TagName.MaxPerTask"/>.</summary>
    public static IReadOnlyList<string> With(IReadOnlyList<string> tags, string typed)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var tag = Normalized([typed], nameof(typed))[0];
        var existing = Read(tags);
        if (existing.Contains(tag))
            return existing;
        string[] added = [.. existing, tag];
        RequireWithinLimit(added, nameof(tags));
        return added;
    }
}
