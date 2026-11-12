namespace Memoria.ToDo.Shared.Sync;

/// <summary>An immutable setup snapshot; reading it performs no file, Git, key-store, or network I/O.</summary>
public interface ISyncSetupState
{
    string? RepositoryFolder { get; }
    string? RepositoryAddress { get; }
    string DeviceName { get; }
    string? PublicKey { get; }
    Uri? AddKeyPage { get; }
    ISshHostKey? HostKey { get; }

    /// <summary>Whether the selected repository and device key have completed their first writable sync.</summary>
    bool IsConfigured { get; }
}
