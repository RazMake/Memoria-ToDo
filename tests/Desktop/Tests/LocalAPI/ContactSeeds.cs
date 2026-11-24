using Memoria.ToDo.Shared.Fakes;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>The contact files the Contacts tests start from, written as literal JSON, and the contract's contact as a plain map.</summary>
internal static class ContactSeeds
{
    public const string TeamFile = TestContactStore.TeamFile;
    public const string AlumniFile = TestContactStore.Folder + "/alumni.json";

    /// <summary>
    /// The groups me (position 100, holding contact 42), team (1000: 1 Ana Popa, alias "ana", and 2 Bob Rus), and alumni (2000: 3 Cris Ionescu),
    /// then the clone is selected.
    /// </summary>
    public static void SeedStandard(this ApiHarness api)
    {
        api.Files.SeedGroup(api.Clone, "team", 1000,
            ContactFiles.Person("1", "Ana Popa", ("alias", "ana"), ("title", "Engineer")),
            ContactFiles.Person("2", "Bob Rus", ("title", "Engineer Lead")));
        api.Files.SeedGroup(api.Clone, "alumni", 2000, ContactFiles.Person("3", "Cris Ionescu"));
        api.Files.SeedMe(api.Clone, 100, ContactFiles.Person("42", "Me Myself", ("title", "Owner")));
        api.LoadContacts();
    }

    /// <summary>A contract contact holding exactly these fields, by their file names.</summary>
    public static V1.Contact Contact(params (string Name, string Value)[] fields)
    {
        var contact = new V1.Contact();
        foreach (var (name, value) in fields)
            contact.Fields[name] = value;
        return contact;
    }

    /// <summary>The contact's fields as a plain map, to compare with what a test expects.</summary>
    public static Dictionary<string, string> Map(this V1.Contact contact) =>
        contact.Fields.ToDictionary(pair => pair.Key, pair => pair.Value);

    /// <summary>The expected map for these fields.</summary>
    public static Dictionary<string, string> Map(params (string Name, string Value)[] fields) =>
        fields.ToDictionary(field => field.Name, field => field.Value);

    /// <summary>The names of the groups in the list, in order.</summary>
    public static List<string> Names(this V1.GroupList list) => [.. list.Groups.Select(group => group.Name)];

    /// <summary>The IDs of the contacts in the list, in order.</summary>
    public static List<string> Ids(this V1.ContactList list) => [.. list.Contacts.Select(contact => contact.Fields["id"])];
}
