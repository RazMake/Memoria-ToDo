namespace Memoria.ToDo.Shared.Tasks;

/// <summary>A color's name or a place's rank key, and when it was set.</summary>
internal sealed record TagStamp(string Value, DateTimeOffset Updated);

/// <summary>
/// One tag's entry: the tag as spelled when last set, and its color and place, each with its own time. At least one is set.
/// </summary>
internal sealed record TagColorEntry(string Tag, TagStamp? Color, TagStamp? Place)
{
    /// <summary>The entry of both, merging color and place apart: the later time wins, and <paramref name="remote"/> on equal times.</summary>
    public TagColorEntry MergedWith(TagColorEntry remote) =>
        new(Latest > remote.Latest ? Tag : remote.Tag, Later(Color, remote.Color), Later(Place, remote.Place));

    private DateTimeOffset Latest => Max(Color, Place);

    private static DateTimeOffset Max(TagStamp? a, TagStamp? b) =>
        a is null ? b?.Updated ?? DateTimeOffset.MinValue : b is null ? a.Updated : a.Updated > b.Updated ? a.Updated : b.Updated;

    private static TagStamp? Later(TagStamp? local, TagStamp? remote) =>
        local is null || remote is not null && local.Updated <= remote.Updated ? remote : local;
}

/// <summary>The color entries of <c>tags.json</c>, one per tag with case ignored. Immutable.</summary>
internal sealed class TagColorSet
{
    public static readonly TagColorSet Empty = new(new Dictionary<string, TagColorEntry>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, TagColorEntry> byTag;

    private TagColorSet(Dictionary<string, TagColorEntry> byTag) => this.byTag = byTag;

    /// <summary>The entries sorted by tag, ignoring case.</summary>
    public IEnumerable<TagColorEntry> Entries => byTag.Values.OrderBy(entry => entry.Tag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Throws <see cref="InvalidDataException"/> when two entries name the same tag.</summary>
    public static TagColorSet Of(IEnumerable<TagColorEntry> entries)
    {
        var byTag = new Dictionary<string, TagColorEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (!byTag.TryAdd(entry.Tag, entry))
                throw new InvalidDataException($"The tag '{entry.Tag}' has two entries.");
        }

        return new TagColorSet(byTag);
    }

    public TagColorEntry? Find(string tag) => byTag.GetValueOrDefault(tag);

    /// <summary>These entries with <paramref name="entries"/> in place of any entry for the same tags.</summary>
    public TagColorSet With(IEnumerable<TagColorEntry> entries)
    {
        var changed = new Dictionary<string, TagColorEntry>(byTag, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
            changed[entry.Tag] = entry;
        return new TagColorSet(changed);
    }

    /// <summary>Every entry of both, merging a tag on both sides entry by entry.</summary>
    public TagColorSet MergedWith(TagColorSet remote)
    {
        var merged = new Dictionary<string, TagColorEntry>(byTag, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in remote.byTag.Values)
            merged[entry.Tag] = merged.TryGetValue(entry.Tag, out var local) ? local.MergedWith(entry) : entry;
        return new TagColorSet(merged);
    }

    /// <summary>
    /// <paramref name="tags"/> in the user's order: the tags with no place first, then the placed tags by place, equal
    /// places and tags with none in alphabetical order, ignoring case.
    /// </summary>
    public List<string> Sort(IEnumerable<string> tags)
    {
        var sorted = tags.ToList();
        sorted.Sort((a, b) =>
        {
            var placeA = Find(a)?.Place?.Value;
            var placeB = Find(b)?.Place?.Value;
            var byPlace = placeA is null || placeB is null
                ? (placeA is null ? 0 : 1) - (placeB is null ? 0 : 1)
                : string.CompareOrdinal(placeA, placeB);
            return byPlace != 0 ? byPlace : StringComparer.OrdinalIgnoreCase.Compare(a, b);
        });
        return sorted;
    }
}
