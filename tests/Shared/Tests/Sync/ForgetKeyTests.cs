using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// Forget key: Sync deletes the device key as a setup action. The selected clone and the trusted hosts stay, and the
/// device isn't configured again until a new key completes a first sync.
/// </summary>
[Unit]
public sealed class ForgetKeyTests
{
    private const string CustomAddress = SyncFixture.ScpAddress;

    private readonly SyncFixture f = new();

    [Fact]
    public async Task Deletes_the_device_key_and_stops_showing_its_public_key()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        Assert.NotNull(sync.Setup.State.PublicKey);

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        Assert.False(f.Keys.HasKey);
        Assert.Equal(1, f.Keys.DeleteCalls);
        Assert.Null(sync.Setup.State.PublicKey);
        Assert.Null(sync.Status.Failure);
    }

    [Fact]
    public async Task Does_nothing_and_reports_no_failure_when_the_device_has_no_key()
    {
        await using var sync = f.Create();

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        Assert.False(f.Keys.HasKey);
        Assert.Null(sync.Setup.State.PublicKey);
        Assert.Null(sync.Status.Failure);
        Assert.Equal(SyncState.Idle, sync.Status.State);
    }

    [Fact]
    public async Task Announces_the_setup_without_the_key_once_it_is_deleted()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        var keyAtLastNotice = "unset";
        sync.Setup.Changed += (_, _) => keyAtLastNotice = sync.Setup.State.PublicKey;

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        Assert.Null(keyAtLastNotice);
    }

    [Fact]
    public async Task A_failed_deletion_keeps_the_key_and_what_the_setup_shows_and_reports_the_error()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        var shown = sync.Setup.State.PublicKey;
        var cause = new IOException("The key file is locked.");
        f.Keys.DeleteFailure = cause;

        var failure = await Record.ExceptionAsync(() => sync.Setup.ForgetKeyAsync(CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.True(ReferenceEquals(cause, io) || ReferenceEquals(cause, io.InnerException));
        Assert.True(f.Keys.HasKey);
        Assert.Equal(shown, sync.Setup.State.PublicKey);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Contains("The key file is locked.", sync.Status.Details);
    }

    [Fact]
    public async Task A_retry_after_a_failed_deletion_deletes_the_key()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        f.Keys.DeleteFailure = new IOException("The key file is locked.");
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.ForgetKeyAsync(CancellationToken.None));
        f.Keys.DeleteFailure = null;

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        Assert.False(f.Keys.HasKey);
        Assert.Null(sync.Setup.State.PublicKey);
    }

    [Fact]
    public async Task A_forgotten_key_is_not_shown_or_counted_as_set_up_when_the_setup_cannot_be_refreshed_afterwards()
    {
        f.Ready();
        await using var sync = f.Create();
        await sync.Setup.SetDeviceNameAsync("Desk", CancellationToken.None);
        await sync.SyncNowAsync(CancellationToken.None);
        Assert.NotNull(sync.Setup.State.PublicKey);
        Assert.True(sync.Setup.State.IsConfigured);
        f.Git.OriginReadFailure = new IOException("The origin can't be read.");

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.ForgetKeyAsync(CancellationToken.None));

        Assert.False(f.Keys.HasKey);
        Assert.Null(sync.Setup.State.PublicKey);
        Assert.False(sync.Setup.State.IsConfigured);
        Assert.Equal("Desk", sync.Setup.State.DeviceName);
        Assert.Equal(f.CloneFolder, sync.Setup.State.RepositoryFolder);
        Assert.Equal(CustomAddress, sync.Setup.State.RepositoryAddress);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
    }

    [Fact]
    public async Task A_key_generated_after_forgetting_shows_the_new_public_key_not_the_old_one()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        var old = sync.Setup.State.PublicKey;
        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        await sync.Setup.GenerateKeyAsync(CancellationToken.None);

        var current = sync.Setup.State.PublicKey;
        Assert.NotNull(old);
        Assert.NotNull(current);
        Assert.NotEqual(old, current);
        Assert.Contains(Convert.ToBase64String(f.Keys.PublicKeyBlob), current);
        Assert.Equal(2, f.Keys.GenerateCalls);
    }

    [Fact]
    public async Task The_device_is_not_configured_after_forgetting_until_a_new_key_completes_a_first_sync()
    {
        f.Ready();
        await using var sync = f.Create();
        await sync.SyncNowAsync(CancellationToken.None);
        Assert.True(sync.Setup.State.IsConfigured);

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);
        Assert.False(sync.Setup.State.IsConfigured);

        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        Assert.False(sync.Setup.State.IsConfigured);

        await sync.SyncNowAsync(CancellationToken.None);
        Assert.True(sync.Setup.State.IsConfigured);
    }

    [Fact]
    public async Task A_sync_the_user_starts_after_forgetting_fails_because_the_device_has_no_key()
    {
        f.Ready();
        await using var sync = f.Create();
        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => sync.SyncNowAsync(CancellationToken.None));

        Assert.Equal("The device has no key yet.", failure.Message);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Keeps_the_selected_clone_and_the_trusted_host_keys()
    {
        f.Ready();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);
        var trustFile = f.ReadDeviceFile("known-hosts.json");
        var hostBefore = Describe(sync.Setup.State.HostKey);

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        Assert.Equal(trustFile, f.ReadDeviceFile("known-hosts.json"));
        Assert.Equal(hostBefore, Describe(sync.Setup.State.HostKey));
        Assert.Equal(f.CloneFolder, f.Tasks.RepositoryFolder);
        Assert.Equal(f.CloneFolder, sync.Setup.State.RepositoryFolder);
        Assert.Equal(CustomAddress, sync.Setup.State.RepositoryAddress);
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        await sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None);
        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
    }

    private static string? Describe(ISshHostKey? host) =>
        host is null ? null : $"{host.Host}:{host.Port} {host.Algorithm} {host.Fingerprint} {host.CanAccept}";

    [Fact]
    public async Task Leaves_the_device_name_and_its_other_files_alone()
    {
        await using var sync = f.Create();
        await sync.Setup.SetDeviceNameAsync("Desk", CancellationToken.None);
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        var before = f.Files.AllFilesUnder(f.DeviceFolder);
        var name = f.ReadDeviceFile("device-name.txt");

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        Assert.Equal(before, f.Files.AllFilesUnder(f.DeviceFolder));
        Assert.Equal(name, f.ReadDeviceFile("device-name.txt"));
        Assert.Equal("Desk", sync.Setup.State.DeviceName);
    }

    [Fact]
    public async Task Waits_for_a_running_sync_and_its_key_handle_before_deleting_the_key()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        held.HoldTransportCleanup();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        var cleanupEnded = false;
        var deletedAfterCleanup = false;
        var handlesOpenAtDelete = -1;
        f.Keys.OnDelete = () =>
        {
            deletedAfterCleanup = cleanupEnded;
            handlesOpenAtDelete = f.Keys.OpenHandles;
        };
        var forgetting = StartForgetting(sync, held);

        held.Complete();
        cleanupEnded = true;
        held.FinishCleanup();
        await running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await forgetting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.True(deletedAfterCleanup);
        Assert.Equal(0, handlesOpenAtDelete);
        Assert.False(f.Keys.HasKey);
    }

    [Fact]
    public async Task A_cancelled_request_deletes_nothing()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sync.Setup.ForgetKeyAsync(cancelled.Token));

        Assert.True(f.Keys.HasKey);
        Assert.Equal(0, f.Keys.DeleteCalls);
    }

    [Fact]
    public async Task Fails_with_an_object_disposed_error_once_the_service_is_stopped()
    {
        f.WithKey();
        var sync = f.Create();
        await sync.StopAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.Setup.ForgetKeyAsync(CancellationToken.None));

        Assert.True(f.Keys.HasKey);
        await sync.DisposeAsync();
    }

    // When the call itself fails, the held pass is let go, so stopping the service can't hang on a failing test.
    private static Task StartForgetting(ISyncService sync, FakeGitOperations.PassScript held)
    {
        try
        {
            return sync.Setup.ForgetKeyAsync(CancellationToken.None);
        }
        catch
        {
            held.Complete();
            held.FinishCleanup();
            throw;
        }
    }
}
