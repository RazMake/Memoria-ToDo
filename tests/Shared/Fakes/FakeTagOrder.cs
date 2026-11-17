using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// In-memory <see cref="ITagOrder"/>. Tags match with case ignored. <see cref="Sort"/> shows the tags with no place first,
/// alphabetically, then the placed tags in the order they were placed. The tags last passed to <see cref="Sort"/> are the tags in
/// use. <see cref="Move"/> puts the tag right after its previous neighbor, or right before its next one; when a neighbor has no
/// place, the tags in use, the tag, and its neighbors are placed first, in the order <see cref="Sort"/> shows them.
/// Move(tag, null, null) does nothing. Set <see cref="Failure"/> to make <see cref="Move"/> throw it before keeping
/// anything, as a missing repository or an unreadable tags.json does.
/// </summary>
public sealed class FakeTagOrder : ITagOrder
{
    private readonly List<string> placed = [];
    private IReadOnlyCollection<string> inUse = [];

    /// <summary>When set, <see cref="Move"/> throws it and keeps nothing.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Every move <see cref="Move"/> was asked to make, in order, as called. Failed calls are not listed.</summary>
    public List<(string Tag, string? Previous, string? Next)> Moves { get; } = [];

    /// <summary>Gives these tags places, in this order, without a call being recorded, as an earlier session or another device would.</summary>
    public void Seed(params string[] tagsInOrder)
    {
        placed.Clear();
        placed.AddRange(tagsInOrder);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Sort(IReadOnlyCollection<string> tags)
    {
        inUse = [.. tags];
        var unplaced = tags.Where(tag => IndexOf(tag) < 0).OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase);
        var inPlace = placed.Select(place => tags.FirstOrDefault(tag => Same(tag, place))).OfType<string>();
        return [.. unplaced, .. inPlace];
    }

    /// <inheritdoc />
    public void Move(string tag, string? previousTag, string? nextTag)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

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
                if (IndexOf(known) < 0 && !newcomers.Any(newcomer => Same(newcomer, known)))
                {
                    newcomers.Add(known);
                }
            }

            placed.InsertRange(0, newcomers.OrderBy(newcomer => newcomer, StringComparer.OrdinalIgnoreCase));
        }

        var at = IndexOf(tag);
        if (at >= 0)
        {
            placed.RemoveAt(at);
        }

        var to = previousTag is not null ? IndexOf(previousTag) + 1 : IndexOf(nextTag!);
        placed.Insert(to, tag);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> on the calling thread, as a reload does.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private int IndexOf(string tag) => placed.FindIndex(place => Same(place, tag));
}
