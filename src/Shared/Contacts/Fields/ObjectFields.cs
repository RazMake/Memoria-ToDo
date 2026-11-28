using System.Text.Json;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>A JSON object's properties in file order, each a standalone value. The last of a repeated name wins.</summary>
internal sealed class ObjectFields
{
    private readonly List<KeyValuePair<string, JsonElement>> pairs;

    private ObjectFields(List<KeyValuePair<string, JsonElement>> pairs) => this.pairs = pairs;

    public static ObjectFields Empty => new([]);

    public IReadOnlyList<KeyValuePair<string, JsonElement>> Pairs => pairs;

    public static ObjectFields Read(JsonElement obj) =>
        Of(obj.EnumerateObject().Select(property => KeyValuePair.Create(property.Name, property.Value.Clone())));

    public static ObjectFields Of(IEnumerable<KeyValuePair<string, JsonElement>> pairs)
    {
        var result = new List<KeyValuePair<string, JsonElement>>();
        foreach (var pair in pairs)
        {
            var at = result.FindIndex(existing => existing.Key == pair.Key);
            if (at < 0)
                result.Add(pair);
            else
                result[at] = pair;
        }

        return new ObjectFields(result);
    }

    public JsonElement? Get(string name)
    {
        foreach (var pair in pairs)
            if (pair.Key == name)
                return pair.Value;
        return null;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (name, value) in pairs)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>Whether both hold the same properties with the same values, in any order.</summary>
    public bool SameAs(ObjectFields other) =>
        pairs.Count == other.pairs.Count && pairs.All(pair => JsonValues.Equal(pair.Value, other.Get(pair.Key)));

    /// <summary>
    /// Three-way merge by property: one side's change wins over an unchanged side, and the remote wins when both changed.
    /// Keeps the remote's order, then the properties only the local side has. A null basis has no properties.
    /// </summary>
    public static ObjectFields Merge(ObjectFields? basis, ObjectFields local, ObjectFields remote)
    {
        var names = remote.pairs.Select(pair => pair.Key)
            .Concat(local.pairs.Select(pair => pair.Key).Where(name => remote.Get(name) is null));
        var merged = new List<KeyValuePair<string, JsonElement>>();
        foreach (var name in names)
        {
            var (b, l, r) = (basis?.Get(name), local.Get(name), remote.Get(name));
            var winner = JsonValues.Equal(b, r) ? l : r;
            if (winner is { } value)
                merged.Add(KeyValuePair.Create(name, value));
        }

        return new ObjectFields(merged);
    }
}
