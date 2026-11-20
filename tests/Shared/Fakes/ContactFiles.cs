using System.Text;
using System.Text.Json;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>Contact files as literal JSON, written and read here so no test depends on how Contacts writes them.</summary>
public static class ContactFiles
{
    public static string Quote(string text) => JsonSerializer.Serialize(text);

    /// <summary>A JSON object of text fields, written on one line.</summary>
    public static string Object(params (string Key, string Value)[] fields) =>
        "{ " + string.Join(", ", fields.Select(field => $"{Quote(field.Key)}: {Quote(field.Value)}")) + " }";

    /// <summary>A group file's member: the required fields, then <paramref name="fields"/>.</summary>
    public static string Person(string id, string fullName, params (string Key, string Value)[] fields) =>
        Object([("id", id), ("fullName", fullName), .. fields]);

    public static string Group(int position, params string[] members) =>
        $$"""{ "version": 1, "position": {{position}}, "members": [ {{string.Join(", ", members)}} ] }""";

    public static string Me(int position, string details) =>
        $$"""{ "version": 1, "position": {{position}}, "details": {{details}} }""";

    /// <summary>A data type file: version 1 and the entries under <paramref name="listName"/> (the file name without ".json").</summary>
    public static string DataType(string listName, params string[] entries) =>
        $$"""{ "version": 1, {{Quote(listName)}}: [ {{string.Join(", ", entries)}} ] }""";

    public static string Templates(params string[] templates) =>
        $$"""{ "version": 1, "templates": [ {{string.Join(", ", templates.Select(Quote))}} ] }""";

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>What a test checks of a file's JSON: each object's fields by name (strings as text, anything else as its JSON), in file order.</summary>
    public sealed record Parsed(int? Position, IReadOnlyList<IReadOnlyDictionary<string, string>> Objects, IReadOnlyDictionary<string, string> Top)
    {
        /// <summary>The object whose <c>id</c> is <paramref name="id"/>.</summary>
        public IReadOnlyDictionary<string, string> WithId(string id) => Objects.Single(o => o["id"] == id);

        public IReadOnlyList<string> Ids => [.. Objects.Select(o => o["id"])];
    }

    /// <summary>A group file's or data type file's content: its objects (the members, the details, or the data type's entries), position, and other top-level fields.</summary>
    public static Parsed Read(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        var objects = new List<IReadOnlyDictionary<string, string>>();
        var top = new Dictionary<string, string>();
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name is "members" or "pronouns" or "careerPaths")
                objects.AddRange(property.Value.EnumerateArray().Select(Fields));
            else if (property.Name == "details")
                objects.Add(Fields(property.Value));
            else
                top[property.Name] = Text(property.Value);
        }

        return new Parsed(root.TryGetProperty("position", out var position) ? position.GetInt32() : null, objects, top);
    }

    public static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private static IReadOnlyDictionary<string, string> Fields(JsonElement element) =>
        element.EnumerateObject().ToDictionary(property => property.Name, property => Text(property.Value));
}
