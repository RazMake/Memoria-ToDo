namespace Memoria.ToDo.Shared.Sync;

/// <summary>An immutable view of the setup; every change publishes a new one.</summary>
internal sealed record SetupSnapshot(
    string? RepositoryFolder,
    string? RepositoryAddress,
    string DeviceName,
    string? PublicKey,
    Uri? AddKeyPage,
    ISshHostKey? HostKey,
    bool IsConfigured) : ISyncSetupState;
