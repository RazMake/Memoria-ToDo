using Serilog;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>
/// The mention templates file: the side that changed more wins, the remote when both changed as much. A side's change from the common
/// version is none, only the layout, only the order, or an edit, from least to most. A side that only reordered or changed the layout
/// counts as unchanged against a deletion. With no common version, the remote wins. The winner's bytes are kept as they are.
/// Each merge logs its choice at Debug.
/// </summary>
internal sealed class TemplatesFileKind(ILogger logger) : ContactFileKind<ParsedTemplates>
{
    public override bool Holds(IReadOnlyList<string> parts) =>
        IsIn(parts, ContactLayout.DataTypesFolderName) && parts[^1] == ContactLayout.TemplatesFileName;

    protected override ParsedTemplates Read(IReadOnlyList<string> parts, byte[] bytes) => ParsedTemplates.Of(bytes);

    protected override byte[] Write(ParsedTemplates file) => file.Bytes;

    protected override bool DeletionWins(IReadOnlyList<string> parts, ParsedTemplates basis, ParsedTemplates kept)
    {
        var change = TemplatesFileFormat.Change(basis, kept);
        var deletionWins = change <= TemplatesChange.Order;
        logger.Debug("Merged {TemplatesFile}, deleted on one side: the other side's change is {Change}, so {Winner}",
            string.Join('/', parts), change, deletionWins ? "the deletion wins" : "that side's file is kept");
        return deletionWins;
    }

    protected override ParsedTemplates MergeSides(IReadOnlyList<string> parts, ParsedTemplates? basis, ParsedTemplates local, ParsedTemplates remote)
    {
        if (basis is null)
        {
            logger.Debug("Merged {TemplatesFile}: there is no common version, so the remote side wins", string.Join('/', parts));
            return remote;
        }

        var (localChange, remoteChange) = (TemplatesFileFormat.Change(basis, local), TemplatesFileFormat.Change(basis, remote));
        var localWins = localChange > remoteChange;
        logger.Debug("Merged {TemplatesFile}: the local side's change is {LocalChange} and the remote's is {RemoteChange}, so the {Winner} side wins",
            string.Join('/', parts), localChange, remoteChange, localWins ? "local" : "remote");
        return localWins ? local : remote;
    }
}
