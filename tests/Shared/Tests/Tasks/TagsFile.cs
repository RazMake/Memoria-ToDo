using System.Text.Json;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>A color or a place in tags.json: its value and the UTC time it was set.</summary>
internal sealed record Stamp(string Value, string Updated);

/// <summary>One tags.json version 2 entry; a null color or place is written as JSON null.</summary>
internal sealed record TagEntry(string Tag, Stamp? Color, Stamp? Place);

/// <summary>Version 2 of tags.json as literal JSON, and a reader of it, so tests don't depend on how Tasks writes the file.</summary>
internal static class TagsFile
{
    public const string Earlier = "2026-01-01T00:00:00.000Z";
    public const string Now = "2026-11-12T08:30:15.123Z";
    public const string T1 = "2026-02-01T00:00:00.000Z";
    public const string T2 = "2026-03-01T00:00:00.000Z";
    public const string T3 = "2026-04-01T00:00:00.000Z";

    public static TagEntry Placed(string tag, string place, string updated = Earlier) => new(tag, null, new Stamp(place, updated));

    public static TagEntry Colored(string tag, string color, string updated = Earlier) => new(tag, new Stamp(color, updated), null);

    public static string Json(params TagEntry[] entries) => Json(2, entries);

    public static string Json(int version, params TagEntry[] entries) =>
        $$"""
        {
          "version": {{version}},
          "tags": [{{string.Join(", ", entries.Select(Entry))}}]
        }
        """;

    public static byte[] Bytes(params TagEntry[] entries) => System.Text.Encoding.UTF8.GetBytes(Json(entries));

    /// <summary>Writes tags.json in the clone as version 2.</summary>
    public static void SeedTags(this TasksFixture f, params TagEntry[] entries) => f.WriteText(f.TagColorsFile, Json(entries));

    /// <summary>Reads tags.json in the clone as version 2 entries, in file order.</summary>
    public static List<TagEntry> ReadTags(this TasksFixture f)
    {
        var (version, entries) = Parse(f.Files.ReadFile(f.TagColorsFile));
        Xunit.Assert.Equal(2, version);
        return entries;
    }

    /// <summary>Counts the color notices, order notices, and local saves raised from now on.</summary>
    public static (Func<int> Colors, Func<int> Order, Func<int> Saved) CountNotices(this TasksFixture f)
    {
        var colors = 0;
        var order = 0;
        var saved = 0;
        f.Store.TagColors.Changed += (_, _) => colors++;
        f.Store.TagOrder.Changed += (_, _) => order++;
        f.Store.Synchronization.LocalSaved += (_, _) => saved++;
        return (() => colors, () => order, () => saved);
    }

    public static (int Version, List<TagEntry> Entries) Parse(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes);
        var entries = json.RootElement.GetProperty("tags").EnumerateArray()
            .Select(e => new TagEntry(e.GetProperty("tag").GetString()!, ReadStamp(e.GetProperty("color")), ReadStamp(e.GetProperty("place"))))
            .ToList();
        return (json.RootElement.GetProperty("version").GetInt32(), entries);
    }

    private static Stamp? ReadStamp(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null
            ? null
            : new Stamp(element.GetProperty("value").GetString()!, element.GetProperty("updated").GetString()!);

    private static string Entry(TagEntry e) =>
        "{ \"tag\": " + Quote(e.Tag) + ", \"color\": " + Stamped(e.Color) + ", \"place\": " + Stamped(e.Place) + " }";

    private static string Stamped(Stamp? s) =>
        s is null ? "null" : "{ \"value\": " + Quote(s.Value) + ", \"updated\": " + Quote(s.Updated) + " }";

    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
