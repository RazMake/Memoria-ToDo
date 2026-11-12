using System.Text;
using LibGit2Sharp;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>A bare repository the test SSH server serves as the shared origin, made and read with real LibGit2Sharp.</summary>
internal sealed class OriginRepository
{
    private const string Main = "refs/heads/main";

    private OriginRepository(string path) => Path = path;

    public string Path { get; }

    /// <summary>Makes an empty bare repository whose default branch, main, does not exist yet.</summary>
    public static OriginRepository CreateUnborn(string path)
    {
        Repository.Init(path, isBare: true);
        File.WriteAllText(System.IO.Path.Combine(path, "HEAD"), "ref: refs/heads/main\n");
        return new OriginRepository(path);
    }

    /// <summary>Makes a bare repository whose main starts with <paramref name="files"/>.</summary>
    public static OriginRepository CreateWith(string path, IReadOnlyDictionary<string, string> files)
    {
        var origin = CreateUnborn(path);
        origin.Commit(files, "Seed");
        return origin;
    }

    /// <summary>Adds a commit on main, as another device's push would, setting each file to the given text.</summary>
    public string Commit(IReadOnlyDictionary<string, string> files, string message)
    {
        using var repo = new Repository(Path);
        var parent = repo.Branches["main"]?.Tip;
        var tree = parent is null ? new TreeDefinition() : TreeDefinition.From(parent);
        foreach (var (file, text) in files)
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
            tree.Add(file, repo.ObjectDatabase.CreateBlob(content), Mode.NonExecutableFile);
        }

        var signature = new Signature("Other device", "other@example.org", DateTimeOffset.UtcNow);
        var commit = repo.ObjectDatabase.CreateCommit(
            signature, signature, message, repo.ObjectDatabase.CreateTree(tree), parent is null ? [] : [parent], prettifyMessage: false);
        repo.Refs.Add(Main, commit.Id, allowOverwrite: true);
        return commit.Sha;
    }

    /// <summary>Adds a commit on main that deletes one file, as another device's push of a deletion would.</summary>
    public string Remove(string file, string message)
    {
        using var repo = new Repository(Path);
        var parent = repo.Branches["main"]!.Tip;
        var tree = TreeDefinition.From(parent);
        tree.Remove(file);
        var signature = new Signature("Other device", "other@example.org", DateTimeOffset.UtcNow);
        var commit = repo.ObjectDatabase.CreateCommit(
            signature, signature, message, repo.ObjectDatabase.CreateTree(tree), [parent], prettifyMessage: false);
        repo.Refs.Add(Main, commit.Id, allowOverwrite: true);
        return commit.Sha;
    }

    /// <summary>The commit main points at, or null while main does not exist.</summary>
    public string? MainTip
    {
        get
        {
            using var repo = new Repository(Path);
            return repo.Branches["main"]?.Tip.Sha;
        }
    }

    /// <summary>Who made the commit main points at and what it says, or null while main does not exist.</summary>
    public (string AuthorName, string AuthorEmail, string Message)? MainTipCommit
    {
        get
        {
            using var repo = new Repository(Path);
            return repo.Branches["main"]?.Tip is { } tip ? (tip.Author.Name, tip.Author.Email, tip.Message) : null;
        }
    }

    public int MainCommitCount
    {
        get
        {
            using var repo = new Repository(Path);
            return repo.Branches["main"]?.Commits.Count() ?? 0;
        }
    }

    /// <summary>The text of a file on main, or null when main or the file does not exist.</summary>
    public string? ReadOnMain(string file)
    {
        using var repo = new Repository(Path);
        return repo.Branches["main"]?.Tip[file]?.Target is Blob blob ? blob.GetContentText() : null;
    }

    /// <summary>The paths of every file on main under <paramref name="folder"/>.</summary>
    public IReadOnlyList<string> FilesOnMain(string folder)
    {
        using var repo = new Repository(Path);
        return repo.Branches["main"]?.Tip[folder]?.Target is Tree tree
            ? [.. tree.Select(entry => entry.Path)]
            : [];
    }
}
