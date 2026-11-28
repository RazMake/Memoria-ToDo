namespace Memoria.ToDo.Shared.Platform;

/// <summary>A path relative to the clone, as Git names it.</summary>
public static class RepositoryPath
{
    /// <summary>The path's parts, split at <c>/</c>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is null.</exception>
    /// <exception cref="ArgumentException">The path is rooted, such as <c>/a</c>, <c>\a</c>, <c>C:/a</c>, or anything this system counts as rooted, or has a <c>.</c> or <c>..</c> part.</exception>
    public static IReadOnlyList<string> Split(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (relativePath.StartsWith('/') || relativePath.StartsWith('\\') || Path.IsPathRooted(relativePath) || relativePath.Length > 1 && relativePath[1] == ':')
            throw new ArgumentException("Must be relative to the repository root.", nameof(relativePath));
        var parts = relativePath.Split('/');
        if (parts.Any(part => part is "." or ".."))
            throw new ArgumentException("Must not contain '.' or '..' segments.", nameof(relativePath));
        return parts;
    }
}
