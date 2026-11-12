using LibGit2Sharp;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Commits the managed files that changed locally. Unmanaged files are never staged, and never committed.</summary>
internal sealed class ManagedCommit(Repository repo, IRepositoryFileRules rules, TimeProvider time, string deviceName)
{
    private const FileStatus StagedMask =
        FileStatus.NewInIndex | FileStatus.ModifiedInIndex | FileStatus.DeletedFromIndex | FileStatus.RenamedInIndex | FileStatus.TypeChangeInIndex;

    private const FileStatus AddedMask = FileStatus.NewInIndex | FileStatus.NewInWorkdir;
    private const FileStatus DeletedMask = FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir;

    /// <summary>Whether any managed file differs from the last commit.</summary>
    public bool HasManagedChanges() => ChangedEntries().Any(entry => IsManaged(entry.FilePath));

    /// <summary>Commits the managed changes; returns false when there were none.</summary>
    public bool Run()
    {
        var lines = new List<string>();
        foreach (var entry in ChangedEntries().ToList())
        {
            if (!IsManaged(entry.FilePath))
            {
                if ((entry.State & StagedMask) != 0)
                {
                    Commands.Unstage(repo, entry.FilePath);
                }

                continue;
            }

            Commands.Stage(repo, entry.FilePath);
            lines.Add($"{Verb(entry.State)}: {entry.FilePath}");
        }

        if (lines.Count == 0)
        {
            return false;
        }

        var author = new Signature(deviceName, "device@memoria.invalid", time.GetUtcNow());
        try
        {
            repo.Commit($"Sync from {deviceName}\n\n{string.Join('\n', lines)}", author, author);
        }
        catch (EmptyCommitException)
        {
            return false;
        }

        return true;
    }

    private bool IsManaged(string path) => rules.Classify(path) != RepositoryFileKind.Unmanaged;

    private IEnumerable<StatusEntry> ChangedEntries() =>
        repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = true })
            .Where(entry => (entry.State & (FileStatus.Unaltered | FileStatus.Ignored)) == 0);

    private static string Verb(FileStatus state) =>
        (state & DeletedMask) != 0 ? "Delete" : (state & AddedMask) != 0 ? "Add" : "Update";
}
