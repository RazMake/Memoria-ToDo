using Grpc.Core;
using Memoria.ToDo.Shared.Contacts;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The Contacts service: maps each call to <see cref="IContacts"/>. It lets a failure escape for <see cref="CallInterceptor"/> to map,
/// except an unknown contact, which is <c>NOT_FOUND</c>.
/// </summary>
internal sealed class ContactsService : V1.Contacts.ContactsBase
{
    private readonly IContacts contacts;

    public ContactsService(IContacts contacts) => this.contacts = contacts;

    /// <inheritdoc />
    public override Task<V1.GroupList> ListGroups(V1.ListGroupsRequest request, ServerCallContext context) =>
        Task.FromResult(Groups());

    /// <inheritdoc />
    public override Task<V1.Contact> GetMe(V1.GetMeRequest request, ServerCallContext context) =>
        Task.FromResult(ContactMessages.Of(contacts.Me ?? throw NotFound("There is no own contact.")));

    /// <inheritdoc />
    public override Task<V1.ContactList> SearchContacts(V1.SearchContactsRequest request, ServerCallContext context)
    {
        var reply = new V1.ContactList();
        foreach (var found in contacts.Search(request.Text))
            reply.Contacts.Add(ContactMessages.Of(found));
        return Task.FromResult(reply);
    }

    /// <inheritdoc />
    public override Task<V1.Contact> GetContact(V1.GetContactRequest request, ServerCallContext context) =>
        Task.FromResult(ContactMessages.Of(Named(request.Name)));

    /// <inheritdoc />
    public override Task<V1.Contact> SaveContact(V1.SaveContactRequest request, ServerCallContext context)
    {
        if (request.Contact is null)
            throw new ArgumentException("SaveContact needs a contact.");

        var contact = ContactMessages.ToContact(request.Contact);
        contacts.Save(request.Group, contact);

        return Task.FromResult(ContactMessages.Of(contact));
    }

    /// <inheritdoc />
    public override Task<V1.DeleteContactResponse> DeleteContact(V1.DeleteContactRequest request, ServerCallContext context)
    {
        // Contacts throws ArgumentException for an unknown ID, which is INVALID_ARGUMENT; the contract says NOT_FOUND.
        // A skipped group isn't loaded, so it reaches Delete, which throws SkippedFileException.
        var group = contacts.Groups.FirstOrDefault(found => found.Name == request.Group);
        if (group is not null && !group.Members.Any(member => member.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            throw NotFound($"The group '{request.Group}' has no contact '{request.Id}'.");

        contacts.Delete(request.Group, request.Id);
        return Task.FromResult(new V1.DeleteContactResponse());
    }

    /// <inheritdoc />
    public override Task<V1.GroupList> MoveGroup(V1.MoveGroupRequest request, ServerCallContext context)
    {
        contacts.MoveGroup(
            request.Group, request.HasPreviousGroup ? request.PreviousGroup : null, request.HasNextGroup ? request.NextGroup : null);
        return Task.FromResult(Groups());
    }

    /// <inheritdoc />
    public override Task<V1.Mention> MentionOf(V1.MentionOfRequest request, ServerCallContext context) =>
        Task.FromResult(new V1.Mention { Markdown = contacts.MentionOf(Named(request.ContactId), request.Template) });

    /// <inheritdoc />
    public override Task WatchContacts(V1.WatchContactsRequest request, IServerStreamWriter<V1.ChangeNotice> responseStream, ServerCallContext context) =>
        ChangeStream.SendNoticesAsync(handler => contacts.Changed += handler, handler => contacts.Changed -= handler, responseStream, context.CancellationToken);

    private Contact Named(string name) => contacts.Find(name) ?? throw NotFound($"There is no contact '{name}'.");

    private static RpcException NotFound(string message) => RpcFailure.Of(StatusCode.NotFound, message);

    private V1.GroupList Groups()
    {
        var reply = new V1.GroupList();
        foreach (var group in contacts.Groups)
        {
            var message = new V1.ContactGroup { Name = group.Name };
            foreach (var member in group.Members)
                message.Contacts.Add(ContactMessages.Of(member));
            reply.Groups.Add(message);
        }

        return reply;
    }
}
