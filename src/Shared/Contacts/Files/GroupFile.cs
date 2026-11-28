using System.Text.Json;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One group file: its name, position, members (one for "me"), and the top-level fields Contacts doesn't know.</summary>
internal sealed record GroupFile(string Name, int Position, IReadOnlyList<StoredContact> Members, ObjectFields Extras)
{
    public bool IsMe => GroupNames.IsMe(Name);

    /// <summary>The group with its members sorted by full name, then ID, ignoring case.</summary>
    public static GroupFile Of(string name, int position, IEnumerable<StoredContact> members, ObjectFields extras) =>
        new(name, position, [.. members.OrderBy(member => member.Contact.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.Contact.Id, StringComparer.OrdinalIgnoreCase)], extras);

    /// <summary>The group from its top-level fields, where "position" is required. Throws <see cref="InvalidDataException"/> for a bad one.</summary>
    public static GroupFile Of(string name, ObjectFields top, IEnumerable<StoredContact> members)
    {
        if (top.Get("position") is not { ValueKind: JsonValueKind.Number } position || !position.TryGetInt32(out var number))
            throw new InvalidDataException("'position' must be a whole number.");
        return Of(name, number, members, ObjectFields.Of(top.Pairs.Where(pair => pair.Key != "position")));
    }

    /// <summary>The position and unknown fields, which merge field by field.</summary>
    public ObjectFields Top() =>
        ObjectFields.Of(new[] { KeyValuePair.Create("position", JsonValues.Number(Position)) }.Concat(Extras.Pairs));

    public GroupFile WithMembers(IEnumerable<StoredContact> members) => Of(Name, Position, members, Extras);

    public GroupFile WithPosition(int position) => this with { Position = position };
}
