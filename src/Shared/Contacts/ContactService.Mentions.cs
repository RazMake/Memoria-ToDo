using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

internal sealed partial class ContactService
{
    public string TextOf(Contact contact, string template)
    {
        ArgumentNullException.ThrowIfNull(contact);
        var parsed = ParseOrThrow(template);
        return parsed.Render(contact, DataTypes);
    }

    public string? TextOf(string address, string template)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(template);
        if (MentionTemplate.Parse(template, out _) is not { } parsed || ContactAddress.IdOf(address) is not { } id)
            return null;
        return repository.Locked(_ => snapshot.Names.Find(id) is { } contact && contact.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
            ? parsed.Render(contact, snapshot.DataTypes)
            : null);
    }

    public string MentionOf(Contact contact, string template) => MentionLink.Of(TextOf(contact, template), contact.Id, template);

    private static MentionTemplate ParseOrThrow(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return MentionTemplate.Parse(template, out var problem) ?? throw new ArgumentException(problem, nameof(template));
    }
}
