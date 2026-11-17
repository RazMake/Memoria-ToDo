using System.Text.Json;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The JSON of <c>tags.json</c>: a version and the entries, sorted by tag. Reads versions 1 and 2, and writes version 2.</summary>
internal static class TagColorsFileFormat
{
    private const int CurrentVersion = 2;

    public static byte[] Write(TagColorSet colors) => JsonFields.Write(writer =>
    {
        writer.WriteNumber("version", CurrentVersion);
        writer.WriteStartArray("tags");
        foreach (var entry in colors.Entries)
        {
            writer.WriteStartObject();
            writer.WriteString("tag", entry.Tag);
            WriteStamp(writer, "color", entry.Color);
            WriteStamp(writer, "place", entry.Place);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    /// <summary>Throws <see cref="InvalidDataException"/> when the file isn't tags this app can read.</summary>
    public static TagColorSet Read(byte[] bytes)
    {
        using var document = JsonFields.Parse(bytes);
        var root = document.RootElement;
        var version = JsonFields.RequireSupportedVersion(root, CurrentVersion);
        return TagColorSet.Of(JsonFields.Objects(root, "tags").Select(entry => version == 1 ? ReadVersion1(entry) : ReadEntry(entry)).ToList());
    }

    private static void WriteStamp(Utf8JsonWriter writer, string name, TagStamp? stamp)
    {
        if (stamp is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteStartObject(name);
        writer.WriteString("value", stamp.Value);
        JsonFields.WriteTime(writer, "updated", stamp.Updated);
        writer.WriteEndObject();
    }

    private static TagColorEntry ReadVersion1(JsonElement entry) =>
        new(ReadTag(entry), ReadColor(JsonFields.String(entry, "color"), JsonFields.Time(entry, "updated")), null);

    private static TagColorEntry ReadEntry(JsonElement entry)
    {
        var tag = ReadTag(entry);
        var color = JsonFields.OptionalObject(entry, "color") is { } colorObject
            ? ReadColor(JsonFields.String(colorObject, "value"), JsonFields.Time(colorObject, "updated"))
            : null;
        var place = JsonFields.OptionalObject(entry, "place") is { } placeObject
            ? ReadPlace(JsonFields.String(placeObject, "value"), JsonFields.Time(placeObject, "updated"))
            : null;
        return color is null && place is null
            ? throw new InvalidDataException($"The tag '{tag}' has neither a color nor a place.")
            : new TagColorEntry(tag, color, place);
    }

    private static string ReadTag(JsonElement entry)
    {
        var tag = JsonFields.String(entry, "tag");
        return TaskText.IsStoredTag(tag) ? tag : throw new InvalidDataException("A tag must be one trimmed, non-empty line.");
    }

    private static TagStamp ReadColor(string color, DateTimeOffset updated) =>
        TaskText.IsSingleLine(color) ? new TagStamp(color, updated) : throw new InvalidDataException("A color must be one non-empty line.");

    private static TagStamp ReadPlace(string place, DateTimeOffset updated)
    {
        RankKey.Require(place);
        return new TagStamp(place, updated);
    }
}
