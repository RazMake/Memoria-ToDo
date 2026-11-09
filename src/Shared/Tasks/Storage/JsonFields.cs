using System.Buffers;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Reading and writing the JSON of task and draft files, so both keep one style and one set of rules.</summary>
internal static class JsonFields
{
    public const int CurrentVersion = 1;

    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The bytes of one JSON object, as <paramref name="fields"/> writes it: UTF-8 with no byte-order mark.</summary>
    public static byte[] Write(Action<Utf8JsonWriter> fields)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            fields(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    public static void WriteTime(Utf8JsonWriter writer, string name, DateTimeOffset time) =>
        writer.WriteString(name, time.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture));

    public static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    /// <summary>Parses a JSON object. Throws <see cref="InvalidDataException"/> for anything else.</summary>
    public static JsonDocument Parse(byte[] bytes)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("Not valid JSON.", e);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new InvalidDataException("Not a JSON object.");
        }

        return document;
    }

    /// <summary>Throws <see cref="InvalidDataException"/> unless the file's <c>version</c> is one this app reads.</summary>
    public static void RequireSupportedVersion(JsonElement obj)
    {
        if (!Field(obj, "version", JsonValueKind.Number).TryGetInt32(out var version) || version < 1 || version > CurrentVersion)
            throw new InvalidDataException("Unsupported version.");
    }

    public static string String(JsonElement obj, string name) => Field(obj, name, JsonValueKind.String).GetString()!;

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
