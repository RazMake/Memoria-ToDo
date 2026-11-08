using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IRepositoryFolder"/>: keeps the folder in memory, remembers each call, and can be
/// told to fail, as when git can't clone or a folder isn't a clone.
/// </summary>
public sealed class FakeRepositoryFolder : IRepositoryFolder
{
    /// <summary>The current folder; null when there's none.</summary>
    public string? Folder { get; set; }

    /// <summary>The URL and folder of each clone, in order, including those that failed.</summary>
    public List<(string Url, string Folder)> Clones { get; } = [];

    /// <summary>When set, <see cref="CloneAsync"/> and <see cref="UseExisting"/> throw it and keep the folder.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Each folder's origin address. A clone adds its folder with the URL it cloned.</summary>
    public Dictionary<string, string> Remotes { get; } = [];

    /// <summary>When set, <see cref="RemoteAsync"/> throws it.</summary>
    public Exception? RemoteFailure { get; set; }

    /// <summary>Each folder's commit identity.</summary>
    public Dictionary<string, string> Identities { get; } = [];

    /// <summary>When set, <see cref="CloneAsync"/> waits for it before it finishes, as a slow clone does.</summary>
    public Task? Pending { get; set; }

    /// <inheritdoc />
    public string? Current() => Folder;

    /// <inheritdoc />
    public Task<string?> IdentityAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Folder is null ? null : Identities.GetValueOrDefault(Folder));

    /// <inheritdoc />
    public Task<string?> RemoteAsync(CancellationToken cancellationToken) =>
        RemoteFailure is not null
            ? Task.FromException<string?>(RemoteFailure)
            : Task.FromResult(Folder is null ? null : Remotes.GetValueOrDefault(Folder));

    /// <inheritdoc />
    public async Task CloneAsync(string url, string folder, CancellationToken cancellationToken)
    {
        Clones.Add((url, folder));
        if (Pending is not null)
        {
            await Pending;
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        Folder = folder;
        Remotes[folder] = url;
    }

    /// <inheritdoc />
    public void UseExisting(string folder)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Folder = folder;
    }
}
