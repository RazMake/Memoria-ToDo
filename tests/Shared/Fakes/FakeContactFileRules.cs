using System.Text;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// Contacts' file rules for tests of Sync: they hold only the paths a test names, record every merge, and merge by a
/// readable rule. A version whose text is <see cref="Unreadable"/> can't be read, so a merge that meets it throws as the real rules do.
/// By default a merge returns its three versions joined with "|", "-" for a missing side, so a test sees that it ran and with which inputs.
/// </summary>
public sealed class FakeContactFileRules : IContactFileRules
{
    /// <summary>The text that stands for a file the rules can't read.</summary>
    public const string Unreadable = "unreadable";

    private readonly HashSet<string> held = new(StringComparer.Ordinal);
    private readonly List<string> merged = [];

    /// <summary>Makes the rules hold these paths.</summary>
    public void Hold(params string[] paths)
    {
        foreach (var path in paths)
            held.Add(path);
    }

    /// <summary>Replaces the default merge. Return null for a merged file that is deleted.</summary>
    public Func<string, byte[]?, byte[]?, byte[]?, byte[]?>? MergeWith { get; set; }

    /// <summary>The path of every merge asked for, in order.</summary>
    public IReadOnlyList<string> MergedPaths
    {
        get { lock (merged) { return [.. merged]; } }
    }

    /// <inheritdoc />
    public bool Holds(string relativePath) => held.Contains(relativePath);

    /// <inheritdoc />
    public byte[]? Merge(string relativePath, byte[]? basis, byte[]? local, byte[]? remote)
    {
        lock (merged)
            merged.Add(relativePath);

        if (new[] { basis, local, remote }.Any(file => file is not null && Encoding.UTF8.GetString(file) == Unreadable))
            throw new InvalidDataException("The contact file can't be read.");

        if (MergeWith is { } custom)
            return custom(relativePath, basis, local, remote);

        return Encoding.UTF8.GetBytes(string.Join('|', new[] { basis, local, remote }.Select(file => file is null ? "-" : Encoding.UTF8.GetString(file))));
    }
}
