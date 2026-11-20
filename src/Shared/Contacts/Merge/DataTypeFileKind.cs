namespace Memoria.ToDo.Shared.Contacts;

/// <summary>A data type file's entries, with the name of the list that holds them.</summary>
internal sealed record DataTypeFile(string ListName, IReadOnlyList<ObjectFields> Entries);

/// <summary>The data type files: entries merge by ID and property, and the remote's property wins when both sides changed it.</summary>
internal sealed class DataTypeFileKind : ContactFileKind<DataTypeFile>
{
    public override bool Holds(IReadOnlyList<string> parts) =>
        IsIn(parts, ContactLayout.DataTypesFolderName) && KeyFields.ForFile(parts[^1]) is not null;

    protected override DataTypeFile Read(IReadOnlyList<string> parts, byte[] bytes)
    {
        var listName = KeyFields.ForFile(parts[^1])!.ListName;
        return new DataTypeFile(listName, DataTypeFileFormat.Read(listName, bytes));
    }

    protected override byte[] Write(DataTypeFile file) => DataTypeFileFormat.Write(file.ListName, file.Entries);

    protected override DataTypeFile MergeSides(DataTypeFile? basis, DataTypeFile local, DataTypeFile remote) =>
        local with { Entries = KeyedMerge.Merge(basis?.Entries, local.Entries, remote.Entries) };
}
