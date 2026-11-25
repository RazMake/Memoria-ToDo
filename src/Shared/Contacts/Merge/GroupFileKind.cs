namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Group files, including "me.json": contacts merge by ID and field, and the position and unknown fields by field.</summary>
internal sealed class GroupFileKind : ContactFileKind<GroupFile>
{
    public override bool Holds(IReadOnlyList<string> parts) =>
        IsIn(parts, ContactLayout.FolderName) && NameOf(parts) is not null;

    protected override GroupFile Read(IReadOnlyList<string> parts, byte[] bytes) => GroupFileFormat.Read(NameOf(parts)!, bytes);

    protected override byte[] Write(GroupFile file) => GroupFileFormat.Write(file);

    protected override GroupFile MergeSides(IReadOnlyList<string> parts, GroupFile? basis, GroupFile local, GroupFile remote)
    {
        var top = ObjectFields.Merge(basis?.Top(), local.Top(), remote.Top());
        var members = local.IsMe
            ? new List<ObjectFields> { ObjectFields.Merge(basis?.Members[0].Fields, local.Members[0].Fields, remote.Members[0].Fields) }
            : KeyedMerge.Merge(basis?.Members.Select(member => member.Fields), local.Members.Select(member => member.Fields),
                remote.Members.Select(member => member.Fields));
        return GroupFile.Of(local.Name, top, members.Select(StoredContact.FromFields));
    }

    private static string? NameOf(IReadOnlyList<string> parts) =>
        parts[^1].EndsWith(ContactLayout.GroupExtension, StringComparison.Ordinal) && parts[^1].Length > ContactLayout.GroupExtension.Length
            ? parts[^1][..^ContactLayout.GroupExtension.Length]
            : null;
}
