namespace Memoria.ToDo.Shared.Contacts;

/// <summary>The mention templates file: the changed side's bytes win, the remote's when both changed.</summary>
internal sealed class TemplatesFileKind : ContactFileKind<byte[]>
{
    public override bool Holds(IReadOnlyList<string> parts) =>
        IsIn(parts, ContactLayout.DataTypesFolderName) && parts[^1] == ContactLayout.TemplatesFileName;

    protected override byte[] Read(IReadOnlyList<string> parts, byte[] bytes)
    {
        TemplatesFileFormat.Read(bytes);
        return bytes;
    }

    protected override byte[] Write(byte[] file) => file;

    protected override byte[] MergeSides(byte[]? basis, byte[] local, byte[] remote) =>
        basis is not null && remote.AsSpan().SequenceEqual(basis) ? local : remote;
}
