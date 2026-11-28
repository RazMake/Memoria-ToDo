using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Sync's rules for contact files, dispatched to the kind of file the path names.</summary>
internal sealed class ContactFileRules(IReadOnlyList<IContactFileKind> kinds) : IContactFileRules
{
    /// <summary>The rules for every kind of contact file there is; the kinds that log their merge choices log to <paramref name="logger"/>.</summary>
    public static ContactFileRules Create(ILogger logger) => new([new GroupFileKind(), new DataTypeFileKind(), new TemplatesFileKind(logger)]);

    public bool Holds(string relativePath) => KindOf(RepositoryPath.Split(relativePath)) is not null;

    public byte[]? Merge(string relativePath, byte[]? basis, byte[]? local, byte[]? remote)
    {
        var parts = RepositoryPath.Split(relativePath);
        var kind = KindOf(parts) ?? throw new ArgumentException($"'{relativePath}' isn't a contact file.", nameof(relativePath));
        return kind.Merge(parts, basis, local, remote);
    }

    private IContactFileKind? KindOf(IReadOnlyList<string> parts) => kinds.FirstOrDefault(kind => kind.Holds(parts));
}
