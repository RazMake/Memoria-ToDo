namespace Memoria.ToDo.Shared.Tasks;

/// <summary>One tag's color: the tag as spelled when it was picked, the color's name, and when it was picked.</summary>
internal sealed record TagColorEntry(string Tag, string Color, DateTimeOffset Updated);

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

    /// <summary>These entries with <paramref name="entry"/> in place of any entry for the same tag.</summary>
    public TagColorSet With(TagColorEntry entry) => new(new Dictionary<string, TagColorEntry>(byTag, StringComparer.OrdinalIgnoreCase) { [entry.Tag] = entry });

    /// <summary>Every entry of both, taking for a tag on both sides the later one, and <paramref name="remote"/>'s on equal times.</summary>
    public TagColorSet MergedWith(TagColorSet remote)
    {
        var merged = new Dictionary<string, TagColorEntry>(byTag, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in remote.byTag.Values)
        {
            if (!merged.TryGetValue(entry.Tag, out var local) || local.Updated <= entry.Updated)
                merged[entry.Tag] = entry;
        }

        return new TagColorSet(merged);
    }
}
