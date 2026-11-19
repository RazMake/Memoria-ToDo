using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>What the model asks the user; a test sets the answers and reads how often each question showed.</summary>
internal sealed class FakePrompts : IContactPrompts
{
    public LeaveAnswer Leave { get; set; } = LeaveAnswer.Cancel;

    public bool Delete { get; set; }

    /// <summary>Runs while a question shows, as a user's click or a second close would.</summary>
    public Action? WhileAsking { get; set; }

    public int LeaveAsked { get; private set; }

    /// <summary>The full name of each contact a delete was asked about.</summary>
    public List<string> DeleteAsked { get; } = [];

    public LeaveAnswer Ask()
    {
        LeaveAsked++;
        WhileAsking?.Invoke();
        return Leave;
    }

    public bool ConfirmDelete(string fullName)
    {
        DeleteAsked.Add(fullName);
        return Delete;
    }
}

/// <summary>
/// The ports a Contacts window test builds on: the real Contacts over an in-memory file system and a clone folder, so every
/// test seeds literal JSON files, plus the shared fakes a window needs. Shared test setup, not a fake of a port.
/// Nothing is loaded until <see cref="Load"/>, as Contacts reads the clone only when it is selected.
/// </summary>
internal sealed class ContactServices
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "memoria-contact-list-tests");

    public ContactServices()
    {
        Contacts = TestContactStore.Create(Disk).Contacts;
        SaveKeys.Set(ModifierKeys.Control, Key.Enter);
    }

    public FakeFileSystem Disk { get; } = new();

    public IContacts Contacts { get; }

    public string Clone { get; } = Path.Combine(Root, "clone");

    public FakeTheme Theme { get; } = new();

    public FakeTimeProvider Time { get; } = new();

    public CloseWindowKeys CloseWindowKeys { get; } = new();

    public SaveKeys SaveKeys { get; } = new();

    public FakeDragPointer Pointer { get; } = new();

    public FakePrompts Prompts { get; } = new();

    /// <summary>
    /// Me (position 100: Razvan Popov, Director), team (1000: Ana Pop with alias anap, Bob Stan reporting to anap), ex-team (2000: Cris Ilie),
    /// friends (3000: Dan Rus with a location), empty (4000, no members), and the pronoun and career path data types. Loaded.
    /// Ana: Engineer, pronouns she, career path se, reports to razvanp, location Cluj, day start 9:30, level start 2024-03-14.
    /// </summary>
    public static ContactServices Typical()
    {
        var services = new ContactServices();
        services.SeedTypical();
        services.Load();
        return services;
    }

    /// <summary>
    /// The user's own group in a file with the name, such as "Me.json" (position 100: Razvan Popov, Director), and friends (3000: Dan Rus). Loaded.
    /// </summary>
    public static ContactServices OwnFileNamed(string fileName)
    {
        var services = new ContactServices();
        services.Disk.Seed(services.Clone, $"{TestContactStore.Folder}/{fileName}", ContactFiles.Me(100, ContactFiles.Person("razvanp", "Razvan Popov", ("title", "Director"))));
        services.Disk.SeedGroup(services.Clone, "friends", 3000, ContactFiles.Person("dan", "Dan Rus"));
        services.Load();
        return services;
    }

    /// <summary>
    /// Typical, plus ex-team's Sam Alpha and friends' Sam Beta, who share the ID "sam", so Contacts reports a shared name. Loaded.
    /// </summary>
    public static ContactServices WithASharedID()
    {
        var services = new ContactServices();
        services.SeedTypical();
        services.Disk.SeedGroup(services.Clone, "ex-team", 2000, ContactFiles.Person("cris", "Cris Ilie"), ContactFiles.Person("sam", "Sam Alpha"));
        services.Disk.SeedGroup(services.Clone, "friends", 3000, ContactFiles.Person("dan", "Dan Rus", ("title", "Neighbor"), ("location", "Oradea")), ContactFiles.Person("sam", "Sam Beta"));
        services.Load();
        return services;
    }

    /// <summary>Seeds what <see cref="Typical"/> describes, without loading it.</summary>
    public void SeedTypical()
    {
        Disk.SeedMe(Clone, 100, ContactFiles.Person("razvanp", "Razvan Popov", ("title", "Director")));
        Disk.SeedGroup(Clone, "team", 1000,
            ContactFiles.Person("ana", "Ana Pop", ("alias", "anap"), ("title", "Engineer"), ("pronounsKey", "she"), ("careerPathKey", "se"),
                ("reportsTo", "razvanp"), ("location", "Cluj"), ("dayStartTime", "9:30"), ("levelStartDate", "2024-03-14")),
            ContactFiles.Person("bob", "Bob Stan", ("title", "Designer"), ("reportsTo", "anap")));
        Disk.SeedGroup(Clone, "ex-team", 2000, ContactFiles.Person("cris", "Cris Ilie"));
        Disk.SeedGroup(Clone, "friends", 3000, ContactFiles.Person("dan", "Dan Rus", ("title", "Neighbor"), ("location", "Oradea")));
        Disk.SeedGroup(Clone, "empty", 4000);
        Disk.SeedDataTypes(Clone);
    }

    /// <summary>Selects the clone, so Contacts reads the files seeded so far, and raises its Changed.</summary>
    public void Load() => Contacts.UseRepositoryFolder(Clone);

    public ContactGroup Group(string name) => Contacts.Groups.Single(group => group.Name == name);

    public Contact Member(string group, string id) => Group(group).Members.Single(member => member.Id == id);

    public byte[] Bytes(string relative) => Disk.ReadFile(TestContactStore.PathOf(Clone, relative));

    /// <summary>A model on these contacts, which reads them when it is made.</summary>
    public ContactsModel Model() => new(Contacts, Prompts);

    public IContactsWindow CreateWindow() =>
        ContactsWindow.Create(Contacts, Theme, Time, CloseWindowKeys, SaveKeys, Pointer);
}
