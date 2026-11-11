namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The tags in use across the tasks, and the rules that follow from them: spelling, adding, and suggesting.</summary>
internal sealed class TagCatalog
{
    private readonly Dictionary<string, string> spelling;

    private TagCatalog(IReadOnlyList<TagUse> inUse, Dictionary<string, string> spelling)
    {
        InUse = inUse;
        this.spelling = spelling;
    }

    /// <summary>The tags in use, most-used first, ties in alphabetical order.</summary>
    public IReadOnlyList<TagUse> InUse { get; }

    public static TagCatalog Of(IEnumerable<TaskItem> tasks)
    {
        var groups = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var task in tasks)
        {
            foreach (var tag in task.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!groups.TryGetValue(tag, out var spellings))
                    groups[tag] = spellings = new Dictionary<string, int>(StringComparer.Ordinal);
                spellings[tag] = spellings.GetValueOrDefault(tag) + 1;
            }
        }

        var spelling = new Dictionary<string, string>(groups.Count, StringComparer.OrdinalIgnoreCase);
        var inUse = new List<TagUse>(groups.Count);
        foreach (var (key, spellings) in groups)
        {
            var mostUsed = spellings.OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).First().Key;
            spelling[key] = mostUsed;
            inUse.Add(new TagUse(mostUsed, spellings.Values.Sum()));
        }

        inUse.Sort((a, b) => a.Count != b.Count
            ? b.Count.CompareTo(a.Count)
            : StringComparer.OrdinalIgnoreCase.Compare(a.Tag, b.Tag));
        return new TagCatalog(inUse, spelling);
    }

    /// <summary><paramref name="tags"/> with the typed tag added, spelled as in use; the same list when it's already there.</summary>
    public IReadOnlyList<string> With(IReadOnlyList<string> tags, string typed)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var tag = TaskText.SingleLine(typed, nameof(typed));
        return Has(tags, tag) ? tags : [.. tags, spelling.GetValueOrDefault(tag, tag)];
    }

    /// <summary>The tags in use that start with the typed text, ignoring case, except <paramref name="tags"/>.</summary>
    public IReadOnlyList<string> Suggest(string typed, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(tags);
        var prefix = typed.Trim();
        if (prefix.Length == 0)
            return [];
        return InUse
            .Select(use => use.Tag)
            .Where(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !Has(tags, tag))
            .ToList();
    }

    public static bool Has(IReadOnlyList<string> tags, string tag) => tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
}
