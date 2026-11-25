namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The tags in use across the tasks, and suggesting from them.</summary>
internal sealed class TagCatalog
{
    private TagCatalog(IReadOnlyList<TagUse> inUse) => InUse = inUse;

    /// <summary>The tags in use, most-used first, ties in ordinal order. Every tag of every task counts, first or not.</summary>
    public IReadOnlyList<TagUse> InUse { get; }

    public static TagCatalog Of(IEnumerable<TaskItem> tasks)
    {
        var counts = new Dictionary<string, int>();
        foreach (var tag in tasks.SelectMany(task => task.Tags))
            counts[tag] = counts.GetValueOrDefault(tag) + 1;

        var inUse = counts.Select(count => new TagUse(count.Key, count.Value)).ToList();
        inUse.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : string.CompareOrdinal(a.Tag, b.Tag));
        return new TagCatalog(inUse);
    }

    /// <summary>The tags in use that contain the normalized typed text anywhere, except <paramref name="tags"/>, normalized.</summary>
    public IReadOnlyList<string> Suggest(string typed, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(tags);
        var text = TagName.Normalize(typed);
        if (text.Length == 0)
            return [];
        var own = TagList.Read(tags);
        return InUse
            .Select(use => use.Tag)
            .Where(tag => tag.Contains(text, StringComparison.Ordinal) && !own.Contains(tag))
            .ToList();
    }
}
