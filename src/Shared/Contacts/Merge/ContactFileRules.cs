using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Sync's rules for contact files, dispatched to the kind of file the path names.</summary>
internal sealed class ContactFileRules(IReadOnlyList<IContactFileKind> kinds) : IContactFileRules
{
    /// <summary>The rules for every kind of contact file there is.</summary>
    public static ContactFileRules Create() => new([new GroupFileKind(), new DataTypeFileKind(), new TemplatesFileKind()]);

    public bool Holds(string relativePath) => KindOf(RepositoryPath.Split(relativePath)) is not null;

    public byte[]? Merge(string relativePath, byte[]? basis, byte[]? local, byte[]? remote)
    {
        var parts = RepositoryPath.Split(relativePath);
        var kind = KindOf(parts) ?? throw new ArgumentException($"'{relativePath}' isn't a contact file.", nameof(relativePath));
        return kind.Merge(parts, basis, local, remote);
    }

    private IContactFileKind? KindOf(IReadOnlyList<string> parts) => kinds.FirstOrDefault(kind => kind.Holds(parts));
}
