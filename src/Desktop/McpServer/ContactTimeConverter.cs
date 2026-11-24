using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>Writes a time of day the way a contact's dayStartTime is written, HH:mm, which is also what save_contact accepts.</summary>
internal sealed class ContactTimeConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ContactTime.TryRead(reader.GetString(), out var time) ? time : throw new JsonException("A time must be written HH:mm.");

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ContactTime.TextOf(value));
}
