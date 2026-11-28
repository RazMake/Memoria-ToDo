namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Merges lists of JSON objects that each have a text "id", the way a group file's members and a data type's entries do.</summary>
internal static class KeyedMerge
{
    /// <summary>
    /// Three-way by ID (ignoring case) and property. Keeps the remote's order, then the local-only objects.
    /// An object only one side has is dropped when the other side deleted it unchanged, and kept when it was edited or added.
    /// </summary>
    public static List<ObjectFields> Merge(IEnumerable<ObjectFields>? basis, IEnumerable<ObjectFields> local, IEnumerable<ObjectFields> remote)
    {
        var basisById = ById(basis ?? []);
        var localById = ById(local);
        var remoteList = remote.ToList();
        var remoteById = ById(remoteList);
        var merged = new List<ObjectFields>();
        foreach (var theirs in remoteList)
        {
            var id = IdOf(theirs);
            basisById.TryGetValue(id, out var original);
            if (localById.TryGetValue(id, out var mine))
                merged.Add(ObjectFields.Merge(original, mine, theirs));
            else if (original is null || !original.SameAs(theirs))
                merged.Add(theirs);
        }

        foreach (var mine in local.Where(candidate => !remoteById.ContainsKey(IdOf(candidate))))
            if (!basisById.TryGetValue(IdOf(mine), out var original) || !original.SameAs(mine))
                merged.Add(mine);
        return merged;
    }

    private static string IdOf(ObjectFields item) => item.Get("id")!.Value.GetString()!;

    private static Dictionary<string, ObjectFields> ById(IEnumerable<ObjectFields> items) =>
        items.ToDictionary(IdOf, StringComparer.OrdinalIgnoreCase);
}
