using System.Runtime.CompilerServices;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// Contacts the way an app's composition root makes it: joined to a synced repository, which selects the folder.
/// A test that needs a contact store creates it here, seeds contact files into the file system under a clone folder,
/// then selects that folder with <see cref="UseRepositoryFolder"/>, which selects through the store's repository.
/// The files are literal JSON from <see cref="ContactFiles"/>, so a test never depends on how Contacts writes them.
/// </summary>
public static class TestContactStore
{
    public const string Folder = "autocomplete/contacts";
    public const string TeamFile = Folder + "/team.json";
    public const string MeFile = Folder + "/me.json";
    public const string PronounsFile = Folder + "/dataTypes/pronouns.json";
    public const string CareerPathsFile = Folder + "/dataTypes/careerPaths.json";
    public const string TemplatesFile = Folder + "/dataTypes/mentionTemplates.json";

    private static readonly ConditionalWeakTable<IContacts, SyncedRepository> Repositories = new();

    /// <summary>Creates Contacts over <paramref name="repository"/>, or over a new repository when none is given.</summary>
    public static IContactStore Create(IFileSystem files, SyncedRepository? repository = null)
    {
        repository ??= new SyncedRepository();
        var store = ContactStore.Create(files, repository);
        Repositories.Add(store.Contacts, repository);
        return store;
    }

    /// <summary>The repository the contacts were created over.</summary>
    public static SyncedRepository RepositoryOf(this IContacts contacts) =>
        Repositories.TryGetValue(contacts, out var repository)
            ? repository
            : throw new InvalidOperationException("The store wasn't created by TestContactStore.");

    /// <summary>Selects the clone through the store's repository, so Contacts reads the folder; null clears the selection.</summary>
    public static void UseRepositoryFolder(this IContacts contacts, string? folder) => contacts.RepositoryOf().Select(folder);

    /// <summary>The full path of a clone-relative path (written with "/") in <paramref name="clone"/>.</summary>
    public static string PathOf(string clone, string relative) => Path.Combine(clone, relative.Replace('/', Path.DirectorySeparatorChar));

    public static void Seed(this IFileSystem files, string clone, string relative, string text) =>
        files.SeedBytes(clone, relative, ContactFiles.Utf8(text));

    public static void SeedBytes(this IFileSystem files, string clone, string relative, byte[] bytes) =>
        files.WriteFile(PathOf(clone, relative), bytes);

    /// <summary>Seeds <c>autocomplete/contacts/&lt;name&gt;.json</c> as a group with the members, given as JSON objects, and returns its clone-relative path.</summary>
    public static string SeedGroup(this IFileSystem files, string clone, string name, int position, params string[] members)
    {
        var relative = $"{Folder}/{name}.json";
        files.Seed(clone, relative, ContactFiles.Group(position, members));
        return relative;
    }

    /// <summary>
    /// Seeds a "team" group with the members, and the mention templates when given (else Contacts' default list), then gives Contacts
    /// the clone folder. A member is <see cref="ContactFiles.Person"/>.
    /// </summary>
    public static void UseContacts(this IContacts contacts, IFileSystem files, string clone, string[]? templates, params string[] members)
    {
        files.SeedGroup(clone, "team", 1000, members);
        if (templates is not null)
        {
            files.Seed(clone, TemplatesFile, ContactFiles.Templates(templates));
        }

        contacts.UseRepositoryFolder(clone);
    }

    public static void SeedMe(this IFileSystem files, string clone, int position, string details) =>
        files.Seed(clone, MeFile, ContactFiles.Me(position, details));

    /// <summary>Pronouns "he" and "she" and career paths "em" (Engineering Manager) and "se", with invented properties.</summary>
    public static void SeedDataTypes(this IFileSystem files, string clone)
    {
        files.Seed(clone, PronounsFile, ContactFiles.ArrayOf(
            ContactFiles.Object(("id", "he"), ("subject", "he"), ("object", "him")),
            ContactFiles.Object(("id", "she"), ("subject", "she"), ("object", "her"))));
        files.Seed(clone, CareerPathsFile, ContactFiles.ArrayOf(
            ContactFiles.Object(("id", "em"), ("name", "Engineering Manager"), ("short", "EM")),
            ContactFiles.Object(("id", "se"), ("name", "Software Engineer"), ("short", "SE"))));
    }
}
