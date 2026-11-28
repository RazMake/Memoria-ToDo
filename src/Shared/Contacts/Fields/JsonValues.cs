using System.Buffers;
using System.Text.Json;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Builds and compares standalone JSON values.</summary>
internal static class JsonValues
{
    public static JsonElement Text(string value) => From(writer => writer.WriteStringValue(value));

    public static JsonElement Number(int value) => From(writer => writer.WriteNumberValue(value));

    public static bool Equal(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;
        return a.ValueKind switch
        {
            JsonValueKind.String => string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.Array => a.GetArrayLength() == b.GetArrayLength()
                && a.EnumerateArray().Zip(b.EnumerateArray()).All(pair => Equal(pair.First, pair.Second)),
            JsonValueKind.Object => ObjectFields.Read(a).SameAs(ObjectFields.Read(b)),
            _ => true,
        };
    }

    /// <summary>Whether both are missing, or both are present and equal.</summary>
    public static bool Equal(JsonElement? a, JsonElement? b) =>
        a is null ? b is null : b is not null && Equal(a.Value, b.Value);

    private static JsonElement From(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            write(writer);
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
