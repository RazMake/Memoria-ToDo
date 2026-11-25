using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Creates the contact store an app uses.</summary>
public static class ContactStore
{
    /// <summary>
    /// Creates the store and adds it to the repository's parts, so it loads on every Select and Update.
    /// The store logs its changes to <paramref name="logger"/>.
    /// Must be called before the first Select: throws <see cref="InvalidOperationException"/> when the repository already selected a folder.
    /// </summary>
    public static IContactStore Create(IFileSystem files, IRepositoryAccess repository, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);

        var service = new ContactService(files, repository, logger);
        repository.Add(service);
        return new Store(service, ContactFileRules.Create(logger));
    }

    private sealed record Store(IContacts Contacts, IContactFileRules Files) : IContactStore;
}
