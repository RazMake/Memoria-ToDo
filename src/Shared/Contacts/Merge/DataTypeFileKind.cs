namespace Memoria.ToDo.Shared.Contacts;

/// <summary>The data type files: entries merge by ID and property, and the remote's property wins when both sides changed it.</summary>
internal sealed class DataTypeFileKind : ContactFileKind<IReadOnlyList<ObjectFields>>
{
    public override bool Holds(IReadOnlyList<string> parts) =>
        IsIn(parts, ContactLayout.DataTypesFolderName) && KeyFields.ForFile(parts[^1]) is not null;

    protected override IReadOnlyList<ObjectFields> Read(IReadOnlyList<string> parts, byte[] bytes) => DataTypeFileFormat.Read(bytes);

    protected override byte[] Write(IReadOnlyList<ObjectFields> file) => DataTypeFileFormat.Write(file);

    protected override IReadOnlyList<ObjectFields> MergeSides(
        IReadOnlyList<ObjectFields>? basis, IReadOnlyList<ObjectFields> local, IReadOnlyList<ObjectFields> remote) =>
        KeyedMerge.Merge(basis, local, remote);
}
