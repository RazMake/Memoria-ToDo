using System.Text.Json;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>
/// Reads and writes a data type file: <c>{ "version": 1, "&lt;list&gt;": [ entries ] }</c>, the list named after the file,
/// each entry an object with a unique text "id".
/// </summary>
internal static class DataTypeFileFormat
{
    private const int Version = 1;

    /// <summary>
    /// The entries of the list named <paramref name="listName"/>. Throws <see cref="InvalidDataException"/> when the file can't be read,
    /// needs a newer app, has another top-level property, or isn't a list of valid entries.
    /// </summary>
    public static IReadOnlyList<ObjectFields> Read(string listName, byte[] bytes)
    {
        using var document = JsonFile.Parse(bytes);
        var root = document.RootElement;
        JsonFile.RequireVersion(root, Version);
        if (!root.TryGetProperty(listName, out var list) || list.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"'{listName}' must be a list.");
        foreach (var property in root.EnumerateObject())
            if (property.Name != "version" && property.Name != listName)
                throw new InvalidDataException($"'{property.Name}' isn't allowed here; the file holds only 'version' and '{listName}'.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ObjectFields>();
        foreach (var element in list.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("An entry must be an object.");
            var entry = ObjectFields.Read(element);
            if (entry.Get("id") is not { ValueKind: JsonValueKind.String } id || id.GetString() is not { Length: > 0 } text)
                throw new InvalidDataException("An entry needs a text 'id'.");
            if (!ids.Add(text))
                throw new InvalidDataException($"The id '{text}' is in the file twice.");
            foreach (var (name, value) in entry.Pairs)
                if (!IsTextOrTexts(value))
                    throw new InvalidDataException($"'{name}' must be text or a list of text.");
            entries.Add(entry);
        }

        return entries;
    }

    public static byte[] Write(string listName, IEnumerable<ObjectFields> entries) =>
        JsonFile.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteStartArray(listName);
            foreach (var entry in entries)
                entry.WriteTo(writer);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    /// <summary>The entries as screens see them: shown as the "name" or else the ID, with every text property but "id".</summary>
    public static IReadOnlyList<DataTypeEntry> Entries(IReadOnlyList<ObjectFields> entries) =>
        [.. entries.Select(entry =>
        {
            var text = entry.Pairs.Where(pair => pair.Value.ValueKind == JsonValueKind.String && pair.Key != "id")
                .ToDictionary(pair => pair.Key, pair => pair.Value.GetString()!);
            var id = entry.Get("id")!.Value.GetString()!;
            return new DataTypeEntry(id, text.GetValueOrDefault("name") is { Length: > 0 } name ? name : id, text);
        })];

    private static bool IsTextOrTexts(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
        || value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String);
}
