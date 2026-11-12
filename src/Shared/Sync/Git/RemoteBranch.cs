using LibGit2Sharp;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Finds the remote's default branch and whether the remote has any branch at all.</summary>
internal static class RemoteBranch
{
    /// <summary>The default branch name, or null when the remote has no branch yet.</summary>
    public static string? Find(IEnumerable<Reference> remoteReferences, string currentBranch)
    {
        const string Prefix = "refs/heads/";
        var references = remoteReferences.ToList();
        var heads = references.Where(r => r.CanonicalName.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
        if (heads.Count == 0)
        {
            return null;
        }

        var head = references.FirstOrDefault(r => r.CanonicalName == "HEAD")?.TargetIdentifier;
        if (head is not null && head.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return head[Prefix.Length..];
        }

        var candidates = (head is null ? heads : heads.Where(r => r.TargetIdentifier == head)).Select(r => r.CanonicalName[Prefix.Length..]).ToList();
        if (candidates.Count == 0)
        {
            candidates = heads.Select(r => r.CanonicalName[Prefix.Length..]).ToList();
        }

        return candidates.Contains(currentBranch) ? currentBranch
            : candidates.Contains("main") ? "main"
            : candidates.Contains("master") ? "master"
            : candidates[0];
    }
}
