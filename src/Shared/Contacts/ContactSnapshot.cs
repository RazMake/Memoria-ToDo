using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Everything loaded from one clone, never changed after it's built.</summary>
internal sealed class ContactSnapshot
{
    private readonly HashSet<string> skippedGroups;
    private readonly NameIndex names;
    private readonly ContactSearch search;
    private readonly IReadOnlyList<Contact> sorted;

    public static ContactSnapshot Empty { get; } = new(false, [], [], TemplatesFileFormat.Default, [], []);

    public ContactSnapshot(bool hasContactFiles, IEnumerable<GroupFile> groupFiles, IReadOnlyList<DataType> dataTypes,
        IReadOnlyList<string> templates, IReadOnlyList<ISkippedRepositoryFile> skipped, IEnumerable<string> skippedGroups)
    {
        HasContactFiles = hasContactFiles;
        GroupFiles = [.. groupFiles.OrderBy(group => group.Position).ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Name, StringComparer.Ordinal)];
        DataTypes = dataTypes;
        Templates = templates;
        Skipped = skipped;
        this.skippedGroups = new HashSet<string>(skippedGroups, StringComparer.Ordinal);
        Groups = [.. GroupFiles.Select(group => new ContactGroup(group.Name, GroupNames.Heading(group.Name), group.Position,
            GroupNames.IsTeam(group.Name), [.. group.Members.Select(member => member.Contact)]))];
        sorted = [.. GroupFiles.SelectMany(group => group.Members).Select(member => member.Contact)
            .OrderBy(contact => contact.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contact => contact.Id, StringComparer.OrdinalIgnoreCase)];
        names = new NameIndex(sorted);
        search = new ContactSearch(sorted);
        Me = GroupFiles.FirstOrDefault(group => group.IsMe)?.Members[0].Contact;
    }

    public bool HasContactFiles { get; }

    /// <summary>The loaded group files in display order.</summary>
    public IReadOnlyList<GroupFile> GroupFiles { get; }

    public IReadOnlyList<ContactGroup> Groups { get; }

    public Contact? Me { get; }

    public IReadOnlyList<DataType> DataTypes { get; }

    public IReadOnlyList<string> Templates { get; }

    public IReadOnlyList<ISkippedRepositoryFile> Skipped { get; }

    public NameIndex Names => names;

    public ContactSearch Search => search;

    /// <summary>Every contact, by full name, then ID.</summary>
    public IReadOnlyList<Contact> Sorted => sorted;

    public GroupFile? GroupNamed(string name) =>
        GroupFiles.FirstOrDefault(group => group.Name.Equals(name, StringComparison.Ordinal));

    /// <summary>Throws <see cref="SkippedFileException"/>, naming the group's file, when that file exists but was skipped.</summary>
    public void ThrowIfSkipped(string group)
    {
        if (!skippedGroups.Contains(group))
            return;
        var path = ContactLayout.GroupRelativePath(group);
        throw new SkippedFileException(Skipped.Last(file => file.RelativePath == path));
    }

    /// <summary>The snapshot with the mention templates replaced.</summary>
    public ContactSnapshot WithTemplates(IReadOnlyList<string> templates) =>
        new(HasContactFiles, GroupFiles, DataTypes, templates, Skipped, skippedGroups);

    /// <summary>The snapshot with each given group file replacing the loaded one of its name.</summary>
    public ContactSnapshot With(IEnumerable<GroupFile> changed)
    {
        var byName = changed.ToDictionary(group => group.Name,         StringComparer.Ordinal);
        return new ContactSnapshot(HasContactFiles, GroupFiles.Select(group => byName.GetValueOrDefault(group.Name, group)),
            DataTypes, Templates, Skipped, skippedGroups);
    }
}
