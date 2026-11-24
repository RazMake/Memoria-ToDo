using Grpc.Core;
using Memoria.ToDo.Shared.Contacts;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// Turns a contact into the message that carries it and back, through Contacts' own <see cref="ContactFields"/>, so the Local API never
/// lists the fields. A message holds each field Contacts stores, by its file name, and leaves out one the contact doesn't store.
/// </summary>
internal static class ContactMessages
{
    /// <summary>The stored text of each field the contact has; never a fallback such as the first name for a missing nickname.</summary>
    public static V1.Contact Of(Contact contact)
    {
        var message = new V1.Contact();
        foreach (var field in ContactFields.All)
        {
            if (field.TextOf(contact) is { Length: > 0 } text)
                message.Fields[field.Name] = text;
        }

        return message;
    }

    /// <summary>
    /// The contact the message holds. Throws <see cref="ArgumentException"/> for a field name Contacts doesn't know, and an
    /// <see cref="RpcException"/> with <c>INVALID_ARGUMENT</c> for a time or date written in another form.
    /// </summary>
    public static Contact ToContact(V1.Contact message)
    {
        var contact = new Contact("", "");
        foreach (var (name, text) in message.Fields)
        {
            var field = FieldNamed(name) ?? throw new ArgumentException($"'{name}' isn't a contact field.");
            try
            {
                contact = field.With(contact, text);
            }
            catch (FormatException invalid)
            {
                throw RpcFailure.Of(StatusCode.InvalidArgument, invalid.Message);
            }
        }

        return contact;
    }

    private static ContactField? FieldNamed(string name)
    {
        foreach (var field in ContactFields.All)
        {
            if (field.Name == name)
                return field;
        }

        return null;
    }
}
