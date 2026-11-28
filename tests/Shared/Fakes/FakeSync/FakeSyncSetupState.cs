using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Shared.Fakes;

public sealed record FakeSyncSetupState : ISyncSetupState
{
    public string? RepositoryFolder { get; init; }
    public string? RepositoryAddress { get; init; }
    public string DeviceName { get; init; } = "Test device";
    public string? PublicKey { get; init; }
    public Uri? AddKeyPage { get; init; }
    public ISshHostKey? HostKey { get; init; }
    public bool IsConfigured { get; init; } = true;
}
