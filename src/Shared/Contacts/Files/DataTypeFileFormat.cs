using System.Text.Json;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Reads and writes a data type file: a JSON array of entries, each an object with a unique text "id".</summary>
internal static class DataTypeFileFormat
{
    /// <summary>Throws <see cref="InvalidDataException"/> when the file isn't a list of valid entries.</summary>
    public static IReadOnlyList<ObjectFields> Read(byte[] bytes)
    {
        using var document = JsonFile.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Not a JSON array.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ObjectFields>();
        foreach (var element in document.RootElement.EnumerateArray())
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

    public static byte[] Write(IEnumerable<ObjectFields> entries) =>
        JsonFile.Write(writer =>
        {
            writer.WriteStartArray();
            foreach (var entry in entries)
                entry.WriteTo(writer);
            writer.WriteEndArray();
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
