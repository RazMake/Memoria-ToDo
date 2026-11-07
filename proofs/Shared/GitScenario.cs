using System.Diagnostics;
using LibGit2Sharp;
using Renci.SshNet;

namespace Memoria.Proofs;

/// <summary>
/// The proof's round trip against one repository: clone A with the URL form and clone B with the
/// scp form, then commit, push, and fetch in both directions.
/// </summary>
public static class GitScenario
{
    private static readonly Signature Author = new("Memoria proof", "proof@memoria.invalid", DateTimeOffset.Now);

    public static void Run(string urlForm, string scpForm, string workFolder, Action<string> log)
    {
        string a = FirstClone(workFolder);
        string b = Path.Combine(workFolder, "b");

        Step(log, $"clone A with the URL form: {urlForm}", () => Repository.Clone(urlForm, a));
        Step(log, $"clone B with the scp form: {scpForm}", () => Repository.Clone(scpForm, b));
        var fromA = Step(log, "commit and push in A", () => CommitAndPush(a, "A"));
        Step(log, "fetch in B and find A's commit", () => FetchAndFastForward(b, fromA));
        var fromB = Step(log, "commit and push in B", () => CommitAndPush(b, "B"));
        Step(log, "fetch in A and find B's commit", () => FetchAndFastForward(a, fromB));
    }

    /// <summary>The clone <see cref="Run"/> makes first, with the URL form.</summary>
    public static string FirstClone(string workFolder) => Path.Combine(workFolder, "a");

    /// <summary>Checks that the transport refuses a host whose key isn't pinned. Leaves the transport with no pins.</summary>
    public static void RefuseUnpinnedHostKey(string urlForm, string host, IPrivateKeySource key, Action<string> log)
    {
        log("refuse a host key that isn't pinned");
        SshTransport.Use(key, new Dictionary<string, string[]> { [host] = ["not-the-server-key"] });
        try
        {
            Repository.ListRemoteReferences(urlForm).ToList();
            throw new InvalidOperationException("The transport trusted a host key that isn't pinned.");
        }
        catch (LibGit2SharpException ex) when (ex.Message.Contains("isn't pinned", StringComparison.Ordinal))
        {
            log($"  ok: {ex.Message}");
        }
    }

    /// <summary>Writes one file into the clone, commits it, and pushes the current branch.</summary>
    public static Pushed CommitAndPush(string clone, string relativePath, string content, string message)
    {
        using var repo = new Repository(clone);
        string full = Path.Combine(clone, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        Commands.Stage(repo, relativePath);
        var commit = repo.Commit(message, Author, Author);

        string branch = repo.Head.CanonicalName;
        string? refused = null;
        repo.Network.Push(
            repo.Network.Remotes["origin"],
            $"{branch}:{branch}",
            new PushOptions { OnPushStatusError = e => refused = $"{e.Reference}: {e.Message}" });
        return refused is null
            ? new Pushed(commit.Sha, relativePath)
            : throw new InvalidOperationException($"The server refused the push: {refused}");
    }

    private static Pushed CommitAndPush(string clone, string name) =>
        CommitAndPush(
            clone,
            $"{name}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt",
            $"Written by clone {name}.\n",
            $"Proof commit from clone {name}");

    private static bool FetchAndFastForward(string clone, Pushed expected)
    {
        using var repo = new Repository(clone);
        Commands.Fetch(repo, "origin", [], new FetchOptions(), null);

        var tracked = repo.Head.TrackedBranch
            ?? throw new InvalidOperationException($"{repo.Head.FriendlyName} tracks no remote branch.");
        if (tracked.Tip.Sha != expected.Sha)
        {
            throw new InvalidOperationException($"{tracked.FriendlyName} is at {tracked.Tip.Sha}, not {expected.Sha}.");
        }

        repo.Reset(ResetMode.Hard, tracked.Tip);
        return File.Exists(Path.Combine(clone, expected.File))
            ? true
            : throw new InvalidOperationException($"{expected.File} is missing after the fast-forward.");
    }

    private static T Step<T>(Action<string> log, string name, Func<T> action)
    {
        log(name);
        long started = Stopwatch.GetTimestamp();
        T result = action();
        log($"  ok in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        return result;
    }

    public sealed record Pushed(string Sha, string File);
}
