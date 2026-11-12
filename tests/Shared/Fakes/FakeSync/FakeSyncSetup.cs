using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Shared.Fakes;

public sealed class FakeSyncSetup : ISyncSetup
{
    public FakeSyncSetupState Snapshot { get; private set; } = new();
    public ISyncSetupState State => Snapshot;
    public event EventHandler? Changed;
    public Exception? Failure { get; set; }
    public Func<CancellationToken, Task>? BeforeOperation { get; set; }
    public int GenerateCount { get; private set; }
    public List<(string Address, string Folder)> Clones { get; } = [];
    public List<(string Address, string Folder)> Selections { get; } = [];
    public string? AcceptedFingerprint { get; private set; }
    public (string Host, int Port)? ForgottenHost { get; private set; }

    public void Publish(FakeSyncSetupState state)
    {
        Snapshot = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task SetDeviceNameAsync(string name, CancellationToken cancellationToken) =>
        Complete(() => Publish(Snapshot with { DeviceName = name }), cancellationToken);

    public Task GenerateKeyAsync(CancellationToken cancellationToken) =>
        Complete(() =>
        {
            GenerateCount++;
            Publish(Snapshot with { PublicKey = "ssh-rsa fake-test-key Test device" });
        }, cancellationToken);

    public Task CloneAsync(string address, string folder, CancellationToken cancellationToken) =>
        Complete(() =>
        {
            Clones.Add((address, folder));
            Publish(Snapshot with { RepositoryAddress = address, RepositoryFolder = folder });
        }, cancellationToken);

    public Task UseExistingAsync(string address, string folder, CancellationToken cancellationToken) =>
        Complete(() =>
        {
            Selections.Add((address, folder));
            Publish(Snapshot with { RepositoryAddress = address, RepositoryFolder = folder });
        }, cancellationToken);

    public Task AcceptHostKeyAsync(string fingerprint, CancellationToken cancellationToken) =>
        Complete(() => AcceptedFingerprint = fingerprint, cancellationToken);

    public Task ForgetHostKeyAsync(string host, int port, CancellationToken cancellationToken) =>
        Complete(() => ForgottenHost = (host, port), cancellationToken);

    private async Task Complete(Action update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            throw Failure;
        }
        if (BeforeOperation is not null)
        {
            await BeforeOperation(cancellationToken);
        }
        update();
    }
}
