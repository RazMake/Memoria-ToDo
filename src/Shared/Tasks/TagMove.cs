namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The rule for moving a tag between two neighbors in the user's order. Pure: no I/O.</summary>
internal static class TagMove
{
    /// <summary>
    /// The tags with <paramref name="tag"/> placed after <paramref name="previous"/> and before <paramref name="next"/>
    /// (null leaves a side open), or null when it already shows there. Throws <see cref="ArgumentException"/> when
    /// <paramref name="previous"/> doesn't show before <paramref name="next"/>. When a neighbor has no place, or both share
    /// one, first gives every tag in use, every placed tag, <paramref name="tag"/>, and its neighbors a place in the order they show.
    /// </summary>
    public static TagColorSet? Apply(TagColorSet tags, IEnumerable<string> inUse, string tag, string? previous, string? next, DateTimeOffset now)
    {
        var everyTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var each in new[] { tag, previous, next }.OfType<string>().Concat(inUse).Concat(tags.Entries.Where(e => e.Place is not null).Select(e => e.Tag)))
            everyTag.TryAdd(each, each);
        var shown = tags.Sort(everyTag.Values);
        var before = previous is null ? -1 : IndexOf(shown, previous);
        var after = next is null ? shown.Count : IndexOf(shown, next);
        if (before >= after)
            throw new ArgumentException("The previous tag doesn't show before the next.");
        var place = IndexOf(shown, tag);
        if (before < place && place < after)
            return null;

        var placed = NeedsPlaces(tags, previous, next) ? WithPlaces(tags, shown, now) : tags;
        var rank = RankKey.Between(PlaceOf(placed, previous), PlaceOf(placed, next));
        return placed.With([PlacedAt(placed, tag, rank, now) with { Tag = tag }]);
    }

    private static bool NeedsPlaces(TagColorSet tags, string? previous, string? next)
    {
        var previousPlace = PlaceOf(tags, previous);
        var nextPlace = PlaceOf(tags, next);
        return previous is not null && previousPlace is null || next is not null && nextPlace is null || previousPlace is not null && previousPlace == nextPlace;
    }

    private static TagColorSet WithPlaces(TagColorSet tags, List<string> shown, DateTimeOffset now)
    {
        var entries = new List<TagColorEntry>(shown.Count);
        string? rank = null;
        foreach (var each in shown)
        {
            rank = RankKey.Between(rank, null);
            entries.Add(PlacedAt(tags, each, rank, now));
        }

        return tags.With(entries);
    }

    /// <summary>The tag's entry, spelled as it is there, with its place set.</summary>
    private static TagColorEntry PlacedAt(TagColorSet tags, string tag, string rank, DateTimeOffset now) =>
        (tags.Find(tag) ?? new TagColorEntry(tag, null, null)) with { Place = new TagStamp(rank, now) };

    private static string? PlaceOf(TagColorSet tags, string? tag) => tag is null ? null : tags.Find(tag)?.Place?.Value;

    private static int IndexOf(List<string> tags, string tag) => tags.FindIndex(each => string.Equals(each, tag, StringComparison.OrdinalIgnoreCase));
}
