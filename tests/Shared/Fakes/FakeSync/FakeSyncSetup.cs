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
    public int ForgetKeyCount { get; private set; }

    /// <summary>The addresses <see cref="CheckAddress"/> accepts, each with its add-key page, or null when none is known. Any other address is refused.</summary>
    public Dictionary<string, Uri?> AcceptedAddresses { get; } = [];

    /// <summary>Every address <see cref="CheckAddress"/> was asked about, as given.</summary>
    public List<string> AddressChecks { get; } = [];

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


    public Task ForgetKeyAsync(CancellationToken cancellationToken) =>
        Complete(() =>
        {
            ForgetKeyCount++;
            Publish(Snapshot with { PublicKey = null, IsConfigured = false });
        }, cancellationToken);

    public bool CheckAddress(string address, out Uri? addKeyPage)
    {
        AddressChecks.Add(address);
        addKeyPage = null;
        return AcceptedAddresses.TryGetValue(address.Trim(), out addKeyPage);
    }

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
