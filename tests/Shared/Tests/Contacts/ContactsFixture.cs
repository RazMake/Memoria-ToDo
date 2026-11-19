using System.Text;
using System.Text.Json;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Platform.Tests;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>
/// The setup every Contacts test shares: Contacts over the in-memory file system and a real synced repository, a clone folder,
/// helpers that seed contact files as literal JSON, and a log of the notices Contacts and the repository raise.
/// Nothing is loaded until <see cref="Load"/>.
/// </summary>
internal sealed class ContactsFixture
{
    public const string Folder = TestContactStore.Folder;
    public const string TeamFile = TestContactStore.TeamFile;
    public const string MeFile = TestContactStore.MeFile;
    public const string PronounsFile = TestContactStore.PronounsFile;
    public const string CareerPathsFile = TestContactStore.CareerPathsFile;
    public const string TemplatesFile = TestContactStore.TemplatesFile;

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "memoria-contacts-tests");

    private readonly RepositoryRig rig = new();
    private readonly List<string> notices = [];
    private readonly List<int> changedThreads = [];
    private readonly List<bool> changedOutsideLock = [];

    public ContactsFixture()
    {
        Files = new RecordingFileSystem(Disk);
        Store = TestContactStore.Create(Files, Repository);
        Contacts.Changed += (_, _) =>
        {
            var outside = rig.AnotherThreadCanLock();
            lock (notices)
            {
                notices.Add("Changed");
                changedThreads.Add(Environment.CurrentManagedThreadId);
                changedOutsideLock.Add(outside);
            }
        };
        Repository.LocalSaved += (_, _) =>
        {
            lock (notices)
                notices.Add("LocalSaved");
        };
    }

    public FakeFileSystem Disk { get; } = new();
    public RecordingFileSystem Files { get; }
    public SyncedRepository Repository => rig.Repository;
    public IContactStore Store { get; }
    public IContacts Contacts => Store.Contacts;
    public IContactFileRules Rules => Store.Files;

    public string Clone { get; } = Path.Combine(Root, "clone");

    /// <summary>Every notice so far, in order: "Changed" from Contacts, "LocalSaved" from the repository.</summary>
    public List<string> Notices
    {
        get { lock (notices) { return [.. notices]; } }
    }

    /// <summary>The managed thread that raised each "Changed" notice.</summary>
    public List<int> ChangedThreads
    {
        get { lock (notices) { return [.. changedThreads]; } }
    }

    /// <summary>For each "Changed" notice, whether another thread could take the repository's lock while the handler ran.</summary>
    public List<bool> ChangedOutsideLock
    {
        get { lock (notices) { return [.. changedOutsideLock]; } }
    }

    public string PathOf(string relative) => TestContactStore.PathOf(Clone, relative);

    /// <summary>Selects the clone, so Contacts reads the files seeded so far.</summary>
    public void Load() => Contacts.UseRepositoryFolder(Clone);

    public bool Exists(string relative) => Disk.FileExists(PathOf(relative));

    public void Seed(string relative, string text) => Disk.Seed(Clone, relative, text);

    public void SeedBytes(string relative, byte[] bytes) => Disk.SeedBytes(Clone, relative, bytes);

    /// <summary>Seeds <c>autocomplete/contacts/&lt;name&gt;.json</c> as a group with the members, given as JSON objects.</summary>
    public string SeedGroup(string name, int position, params string[] members) => Disk.SeedGroup(Clone, name, position, members);

    public void SeedMe(int position, string details) => Disk.SeedMe(Clone, position, details);

    /// <summary>Pronouns "he" and "she" and career paths "em" (Engineering Manager) and "se", with invented properties.</summary>
    public void SeedDataTypes() => Disk.SeedDataTypes(Clone);

    public byte[] Bytes(string relative) => Disk.ReadFile(PathOf(relative));

    public string Text(string relative) => Encoding.UTF8.GetString(Bytes(relative));

    public ContactFiles.Parsed Parsed(string relative) => ContactFiles.Read(Bytes(relative));

    /// <summary>The property names of the first member (or details) of the file, in file order.</summary>
    public List<string> FirstObjectNames(string relative)
    {
        using var json = JsonDocument.Parse(Bytes(relative));
        var root = json.RootElement;
        var first = root.TryGetProperty("members", out var members) ? members[0] : root.GetProperty("details");
        return [.. first.EnumerateObject().Select(property => property.Name)];
    }

    /// <summary>The top-level property names of the file, in file order.</summary>
    public List<string> TopNames(string relative)
    {
        using var json = JsonDocument.Parse(Bytes(relative));
        return [.. json.RootElement.EnumerateObject().Select(property => property.Name)];
    }

    /// <summary>The clone-relative path of every file written since the last <see cref="ForgetWrites"/>, sorted.</summary>
    public List<string> Written =>
        [.. Files.Changed.Select(path => Path.GetRelativePath(Clone, path).Replace('\\', '/')).Distinct().Order(StringComparer.Ordinal)];

    public void ForgetWrites() => Files.Forget();

    public ContactGroup Group(string name) => Contacts.Groups.Single(group => group.Name == name);

    public Contact Member(string group, string id) => Group(group).Members.Single(member => member.Id == id);

    public static List<string> Ids(IEnumerable<Contact> contacts) => [.. contacts.Select(contact => contact.Id)];

    public List<string> SkippedPaths => [.. Contacts.SkippedFiles.Select(file => file.RelativePath)];

    /// <summary>Every group with its position and members, as text, so a test can compare what Contacts holds before and after.</summary>
    public List<string> Snapshot() =>
        [.. Contacts.Groups.Select(group => $"{group.Name}|{group.Heading}|{group.Position}|{group.IsTeam}|{string.Join(";", group.Members)}")];

    /// <summary>Asserts the file is in the skipped files with a reason and needs no newer app.</summary>
    public void AssertSkipped(string relative)
    {
        var skipped = Assert.Single(Contacts.SkippedFiles, file => file.RelativePath == relative);
        Assert.NotEmpty(skipped.Reason);
        Assert.False(skipped.RequiresUpdate);
    }

    /// <summary>Asserts nothing was written, since the last <see cref="ForgetWrites"/>.</summary>
    public void AssertNothingWritten() => Assert.Empty(Written);
}
