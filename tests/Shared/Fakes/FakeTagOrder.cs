using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// In-memory <see cref="ITagOrder"/>. Tags are normalized with <see cref="TagName.Normalize"/>. <see cref="Sort"/> normalizes the tags, drops repeats, and shows the tags with no place first,
/// ordinally, then the placed tags in the order they were placed. The tags last passed to <see cref="Sort"/> are the tags in
/// use. <see cref="Move"/> puts the tag right after its previous neighbor, or right before its next one; when a neighbor has no
/// place, the tags in use, the tag, and its neighbors are placed first, in the order <see cref="Sort"/> shows them.
/// Move(tag, null, null) does nothing. Set <see cref="Failure"/> to make <see cref="Move"/> fail.
/// </summary>
public sealed class FakeTagOrder : ITagOrder
{
    private readonly List<string> placed = [];
    private IReadOnlyCollection<string> inUse = [];

    /// <summary>
    /// When set, <see cref="Move"/> throws it and keeps nothing, as a missing repository or an unreadable tags.json does,
    /// unless <see cref="SavesBeforeFailing"/> is true.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// When true, <see cref="Move"/> keeps the move and records it, then throws <see cref="Failure"/> without raising
    /// <see cref="Changed"/>, as Tasks does when its notices fail after a save.
    /// </summary>
    public bool SavesBeforeFailing { get; set; }

    /// <summary>
    /// Every move <see cref="Move"/> kept, in order, with the normalized tags, including one it kept before failing
    /// because <see cref="SavesBeforeFailing"/> is true. Calls that failed before keeping anything are not listed.
    /// </summary>
    public List<(string Tag, string? Previous, string? Next)> Moves { get; } = [];

    /// <summary>Gives these tags places, in this order, without a call being recorded, as an earlier session or another device would.</summary>
    public void Seed(params string[] tagsInOrder)
    {
        placed.Clear();
        placed.AddRange(tagsInOrder.Select(TagName.Normalize));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Sort(IReadOnlyCollection<string> tags)
    {
        var normalized = tags.Select(NonEmpty).Distinct(StringComparer.Ordinal).ToList();
        inUse = normalized;
        var unplaced = normalized.Where(tag => IndexOf(tag) < 0).OrderBy(tag => tag, StringComparer.Ordinal);
        var inPlace = placed.Where(normalized.Contains);
        return [.. unplaced, .. inPlace];
    }

    /// <inheritdoc />
    public void Move(string tag, string? previousTag, string? nextTag)
    {
        if (Failure is not null && !SavesBeforeFailing)
        {
            throw Failure;
        }

        tag = NonEmpty(tag);
        previousTag = previousTag is null ? null : TagName.Normalize(previousTag);
        nextTag = nextTag is null ? null : TagName.Normalize(nextTag);
        if (previousTag is null && nextTag is null)
        {
            return;
        }

        Moves.Add((tag, previousTag, nextTag));
        var neighbors = new[] { previousTag, nextTag }.OfType<string>().ToList();
        if (neighbors.Any(neighbor => IndexOf(neighbor) < 0))
        {
            // As the real order does: the tags in use, the tag, and its neighbors get places in the order Sort shows them.
            var newcomers = new List<string>();
            foreach (var known in inUse.Concat([tag]).Concat(neighbors))
            {
                if (IndexOf(known) < 0 && !newcomers.Contains(known))
                {
                    newcomers.Add(known);
                }
            }

            placed.InsertRange(0, newcomers.OrderBy(newcomer => newcomer, StringComparer.Ordinal));
        }

        var at = IndexOf(tag);
        if (at >= 0)
        {
            placed.RemoveAt(at);
        }

        var to = previousTag is not null ? IndexOf(previousTag) + 1 : IndexOf(nextTag!);
        placed.Insert(to, tag);
        if (Failure is not null)
        {
            throw Failure;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> on the calling thread, as a reload does.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string NonEmpty(string tag) =>
        TagName.Normalize(tag) is { Length: > 0 } normalized ? normalized : throw new ArgumentException("A tag can't be empty.", nameof(tag));

    private int IndexOf(string tag) => placed.IndexOf(tag);
}
