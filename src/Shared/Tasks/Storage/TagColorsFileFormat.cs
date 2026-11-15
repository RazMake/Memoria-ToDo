using System.Text.Json;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The JSON of <c>tags.json</c>: a version and the entries, sorted by tag.</summary>
internal static class TagColorsFileFormat
{
    public static byte[] Write(TagColorSet colors) => JsonFields.Write(writer =>
    {
        writer.WriteNumber("version", JsonFields.CurrentVersion);
        writer.WriteStartArray("tags");
        foreach (var entry in colors.Entries)
        {
            writer.WriteStartObject();
            writer.WriteString("tag", entry.Tag);
            writer.WriteString("color", entry.Color);
            JsonFields.WriteTime(writer, "updated", entry.Updated);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    /// <summary>Throws <see cref="InvalidDataException"/> when the file isn't tag colors this app can read.</summary>
    public static TagColorSet Read(byte[] bytes)
    {
        using var document = JsonFields.Parse(bytes);
        var root = document.RootElement;
        JsonFields.RequireSupportedVersion(root);
        return TagColorSet.Of(JsonFields.Objects(root, "tags").Select(ReadEntry).ToList());
    }

    private static TagColorEntry ReadEntry(JsonElement entry)
    {
        var tag = JsonFields.String(entry, "tag");
        if (!TaskText.IsStoredTag(tag))
            throw new InvalidDataException("A tag must be one trimmed, non-empty line.");
        var color = JsonFields.String(entry, "color");
        if (!TaskText.IsSingleLine(color))
            throw new InvalidDataException("A color must be one non-empty line.");
        return new TagColorEntry(tag, color, JsonFields.Time(entry, "updated"));
    }
}
