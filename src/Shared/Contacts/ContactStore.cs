using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Creates the contact store an app uses.</summary>
public static class ContactStore
{
    /// <summary>
    /// Creates the store and adds it to the repository's parts, so it loads on every Select and Update.
    /// Must be called before the first Select: throws <see cref="InvalidOperationException"/> when the repository already selected a folder.
    /// </summary>
    public static IContactStore Create(IFileSystem files, IRepositoryAccess repository)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(repository);

        var service = new ContactService(files, repository);
        repository.Add(service);
        return new Store(service, ContactFileRules.Create());
    }

    private sealed record Store(IContacts Contacts, IContactFileRules Files) : IContactStore;
}
