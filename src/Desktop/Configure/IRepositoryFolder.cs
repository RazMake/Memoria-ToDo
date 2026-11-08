namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The folder of the git clone that holds this device's tasks: a port Configure publishes and the composition root implements.</summary>
public interface IRepositoryFolder
{
    /// <summary>The clone's folder, or null when there's none yet.</summary>
    string? Current();

    /// <summary>
    /// The current clone's origin address, or null when there's no clone or the clone has no origin. Fails with a
    /// message the user can read when git can't read it.
    /// </summary>
    Task<string?> RemoteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Who the current clone's commits are made as, such as "Ana Pop &lt;ana@example.com&gt;", or null when there's no
    /// clone or git has no name or e-mail for it. Fails with a message the user can read when git can't read it.
    /// </summary>
    Task<string?> IdentityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clones the repository at <paramref name="url"/> into <paramref name="folder"/>, then makes it the current one.
    /// Fails with a message the user can read, and keeps the current folder, when the clone fails.
    /// </summary>
    Task CloneAsync(string url, string folder, CancellationToken cancellationToken);

    /// <summary>
    /// Makes <paramref name="folder"/>, a clone the user already has, the current one. Fails with a message the user
    /// can read, and keeps the current folder, when it isn't a git clone.
    /// </summary>
    void UseExisting(string folder);
}
