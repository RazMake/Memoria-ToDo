using System.Text.Json;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Reads and writes a group file's bytes.</summary>
internal static class GroupFileFormat
{
    private const int Version = 1;

    /// <summary>Throws <see cref="InvalidDataException"/> when the file can't be read, needs a newer app, or breaks a rule.</summary>
    public static GroupFile Read(string name, byte[] bytes)
    {
        using var document = JsonFile.Parse(bytes);
        JsonFile.RequireVersion(document.RootElement, Version);
        var top = ObjectFields.Read(document.RootElement);
        var membersName = GroupNames.IsMe(name) ? "details" : "members";
        var members = GroupNames.IsMe(name) ? ReadDetails(top.Get(membersName)) : ReadMembers(top.Get(membersName));
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
            if (!ids.Add(member.Contact.Id))
                throw new InvalidDataException($"The id '{member.Contact.Id}' is in the file twice.");

        return GroupFile.Of(name, ObjectFields.Of(top.Pairs.Where(pair => pair.Key != "version" && pair.Key != membersName)), members);
    }

    public static byte[] Write(GroupFile file) =>
        JsonFile.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteNumber("position", file.Position);
            if (file.IsMe)
            {
                writer.WritePropertyName("details");
                file.Members[0].Fields.WriteTo(writer);
            }
            else
            {
                writer.WriteStartArray("members");
                foreach (var member in file.Members)
                    member.Fields.WriteTo(writer);
                writer.WriteEndArray();
            }

            foreach (var (name, value) in file.Extras.Pairs)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

    private static List<StoredContact> ReadMembers(JsonElement? members)
    {
        if (members is not { ValueKind: JsonValueKind.Array } array)
            throw new InvalidDataException("'members' must be a list.");
        return [.. array.EnumerateArray().Select(ReadContact)];
    }

    private static List<StoredContact> ReadDetails(JsonElement? details) =>
        details is { ValueKind: JsonValueKind.Object } contact
            ? [ReadContact(contact)]
            : throw new InvalidDataException("'details' must be an object.");

    private static StoredContact ReadContact(JsonElement contact) =>
        contact.ValueKind == JsonValueKind.Object
            ? StoredContact.FromFields(ObjectFields.Read(contact))
            : throw new InvalidDataException("A member must be an object.");
}
