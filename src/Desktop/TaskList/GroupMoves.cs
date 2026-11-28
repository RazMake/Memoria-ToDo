namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>A group's new place, as what <see cref="Previous"/> and <see cref="Next"/> are: the tag groups it lands between, null at an end.</summary>
internal sealed record GroupMove(string Tag, string? Previous, string? Next);

/// <summary>What a group's drop asks Tasks' tag order for.</summary>
internal static class GroupMoves
{
    /// <summary>
    /// The move for dropping <paramref name="tag"/> in <paramref name="gap"/>, a position among <paramref name="shownTags"/>, as
    /// <see cref="DropTargets.ForGroup"/> gives it. The neighbors are the shown tag groups on each side of the gap, so groups hidden
    /// by search are never neighbors. A null entry is the "No tag" group, which is never a neighbor.
    /// Null when the gap is the group's own place (its index) or the one after it, which move nothing, or isn't a position among the groups.
    /// </summary>
    public static GroupMove? Of(IReadOnlyList<string?> shownTags, string tag, int gap)
    {
        var own = -1;
        for (var i = 0; i < shownTags.Count; i++)
        {
            if (string.Equals(shownTags[i], tag, StringComparison.Ordinal))
            {
                own = i;
            }
        }

        if (gap < 0 || gap > shownTags.Count || own >= 0 && (gap == own || gap == own + 1))
        {
            return null;
        }

        return new GroupMove(tag, gap > 0 ? shownTags[gap - 1] : null, gap < shownTags.Count ? shownTags[gap] : null);
    }
}
