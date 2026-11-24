using System.Collections.Concurrent;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// Reads the IDs a request names, so a log entry can say them for any call: its <c>id</c> and <c>*_id</c> fields, plus what a request
/// registers in <see cref="Extra"/>. Never other contact data.
/// </summary>
internal static class RequestIds
{
    private static readonly ConcurrentDictionary<MessageDescriptor, FieldDescriptor[]> IdFields = new();

    // Requests whose ID isn't in an id field: GetContact's key is an ID or an alias, as sent; SaveContact's is the contact's own id.
    private static readonly Dictionary<Type, Func<IMessage, string>> Extra = new()
    {
        [typeof(V1.GetContactRequest)] = request => ((V1.GetContactRequest)request).Name,
        [typeof(V1.SaveContactRequest)] = request =>
            ((V1.SaveContactRequest)request).Contact?.Fields.GetValueOrDefault("id") ?? "",
    };

    /// <summary>The IDs the request sets, comma-separated; empty when it names none.</summary>
    public static string Of(object request)
    {
        if (request is not IMessage message)
            return "";

        var fields = IdFields.GetOrAdd(message.Descriptor, descriptor =>
            [.. descriptor.Fields.InDeclarationOrder().Where(IsId)]);
        var ids = fields.Select(field => (string)field.Accessor.GetValue(message));
        if (Extra.TryGetValue(message.GetType(), out var extra))
            ids = ids.Append(extra(message));
        return string.Join(", ", ids.Where(id => id.Length > 0));
    }

    private static bool IsId(FieldDescriptor field) =>
        field.FieldType == FieldType.String && (field.Name == "id" || field.Name.EndsWith("_id", StringComparison.Ordinal));
}
