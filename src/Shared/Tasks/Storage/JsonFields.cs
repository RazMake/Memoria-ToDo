using System.Globalization;
using System.Text.Json;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Tasks' fields in the JSON of task, draft, and settings files, on the platform's <see cref="JsonFile"/> style and rules.</summary>
internal static class JsonFields
{
    public const int CurrentVersion = 1;

    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>The bytes of one JSON object, as <paramref name="fields"/> writes it.</summary>
    public static byte[] Write(Action<Utf8JsonWriter> fields) => JsonFile.Write(writer =>
    {
        writer.WriteStartObject();
        fields(writer);
        writer.WriteEndObject();
    });

    public static void WriteTime(Utf8JsonWriter writer, string name, DateTimeOffset time) =>
        writer.WriteString(name, time.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture));

    public static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    /// <summary>The file's <c>version</c>, which is at least 1 and at most <see cref="CurrentVersion"/>. Throws <see cref="InvalidDataException"/> otherwise.</summary>
    public static void RequireSupportedVersion(JsonElement obj) => JsonFile.RequireVersion(obj, CurrentVersion);

    /// <summary>The object, or null when the field is <c>null</c>. The field must be there.</summary>
    public static JsonElement? OptionalObject(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Null ? null : Object(obj, name);

    public static string String(JsonElement obj, string name) => Field(obj, name, JsonValueKind.String).GetString()!;

    public static JsonElement Object(JsonElement obj, string name) => Field(obj, name, JsonValueKind.Object);

    /// <summary>The objects of an array field. Throws <see cref="InvalidDataException"/> when the field isn't an array of objects.</summary>
    public static IEnumerable<JsonElement> Objects(JsonElement obj, string name)
    {
        foreach (var item in Field(obj, name, JsonValueKind.Array).EnumerateArray())
            yield return item.ValueKind == JsonValueKind.Object ? item : throw new InvalidDataException($"'{name}' must hold objects.");
    }

    public static int Int32(JsonElement obj, string name) =>
        Field(obj, name, JsonValueKind.Number).TryGetInt32(out var value) ? value : throw new InvalidDataException($"'{name}' isn't a whole number.");

    /// <summary>A string, or null when the field is <c>null</c>. The field must be there.</summary>
    public static string? OptionalString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Null ? null : String(obj, name);

    public static DateTimeOffset Time(JsonElement obj, string name) => ParseTime(String(obj, name), name);

    /// <summary>A time, or null when the field is <c>null</c>. The field must be there.</summary>
    public static DateTimeOffset? OptionalTime(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Null ? null : Time(obj, name);

    public static string[] Strings(JsonElement obj, string name) =>
        Field(obj, name, JsonValueKind.Array).EnumerateArray().Select(item =>
            item.ValueKind == JsonValueKind.String ? item.GetString()! : throw new InvalidDataException($"'{name}' must hold strings.")).ToArray();

    private static JsonElement Field(JsonElement obj, string name, JsonValueKind kind) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == kind
            ? value
            : throw new InvalidDataException($"Missing or invalid '{name}'.");

    private static DateTimeOffset ParseTime(string text, string name) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : throw new InvalidDataException($"'{name}' isn't a time.");
}
