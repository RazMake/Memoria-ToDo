using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>
/// The <see cref="IContacts"/> an app gets from <see cref="ContactStore"/>, and a part of the synced repository.
/// Every call runs under the repository's lock.
/// </summary>
internal sealed partial class ContactService(IFileSystem files, IRepositoryAccess repository, ILogger logger) : IContacts, IRepositoryPart
{
    private readonly ILogger logger = logger;
    private readonly ContactFolder folder = new(files);
    private ContactSnapshot snapshot = ContactSnapshot.Empty;

    public event EventHandler? Changed;

    public bool HasContactFiles => repository.Locked(_ => snapshot.HasContactFiles);

    public IReadOnlyList<ContactGroup> Groups => repository.Locked(_ => snapshot.Groups);

    public Contact? Me => repository.Locked(_ => snapshot.Me);

    public IReadOnlyList<string> SharedNames => repository.Locked(_ => snapshot.Names.Shared());

    public IReadOnlyList<string> MentionTemplates => repository.Locked(_ => snapshot.Templates);

    public IReadOnlyList<DataType> DataTypes => repository.Locked(_ => snapshot.DataTypes);

    public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles => repository.Locked(_ => snapshot.Skipped);

    Action IRepositoryPart.Load(string? root)
    {
        var loaded = root is null ? ContactSnapshot.Empty : folder.Read(root);
        return () => snapshot = loaded;
    }

    void IRepositoryPart.Loaded() => Changed?.Invoke(this, EventArgs.Empty);

    public Contact? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return repository.Locked(_ => snapshot.Names.Find(name));
    }

    public Contact? ManagerOf(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return contact.ReportsTo is { } name ? Find(name) : null;
    }

    public IReadOnlyList<Contact> DirectReportsOf(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return repository.Locked(_ => (IReadOnlyList<Contact>)
        [.. snapshot.Sorted.Where(candidate => candidate.ReportsTo is { } name
            && snapshot.Names.Find(name) is { } manager
            && manager.Equals(contact))]);
    }

    public IReadOnlyList<Contact> Search(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return repository.Locked(_ => snapshot.Search.Find(text));
    }
}
