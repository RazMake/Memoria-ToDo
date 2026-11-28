using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// What Configure logs through the logger it was given: every failure it catches and shows is also logged at Error with
/// the exception, so the log says what the window only summarizes. The Auto update and Start with Windows failures are
/// checked in UpdateCardTests and StartWithWindowsRowTests; a working window logging no problem, in the paths.
/// </summary>
[Unit]
public class ConfigureErrorLoggingTests : IDisposable
{
    private readonly InMemoryLogSink _log = new();
    private readonly FakeSyncService _sync = new();
    private readonly FakeSetupActions _actions = new();
    private readonly SyncUi _ui;

    public ConfigureErrorLoggingTests()
    {
        _ui = new SyncUi(_sync, _actions, post => post(), _log.CreateLogger());
    }

    public void Dispose() => _ui.Dispose();

    private void AssertErrorWith(Exception failure) =>
        Assert.Contains(_log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));

    [Fact]
    public void A_failed_Copy_public_key_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("Clipboard is busy.");
        _sync.SetupControl.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key Desk", DeviceName = "Desk" });
        _actions.CopyFailure = failure;

        new DeviceActions(_ui, () => { }).CopyPublicKey();

        AssertErrorWith(failure);
    }

    [Fact]
    public async Task A_failed_Generate_key_is_logged_as_an_error_with_the_exception()
    {
        var failure = new IOException("The key file is locked.");
        _sync.SetupControl.Failure = failure;

        await new DeviceActions(_ui, () => { }).GenerateKeyAsync();

        AssertErrorWith(failure);
    }

    [Fact]
    public void A_failed_Set_key_copy_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("Clipboard is busy.");
        const string address = "git@github.com:me/tasks.git";
        _sync.SetupControl.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key Desk", DeviceName = "Desk" });
        _sync.SetupControl.AcceptedAddresses[address] = new Uri("https://github.com/me/tasks/settings/keys/new");
        _actions.CopyFailure = failure;

        new RepositoryChange(_ui, () => { }).SetKey(address);

        AssertErrorWith(failure);
    }

    [Fact]
    public void A_deletion_delay_kept_before_a_notice_failed_is_logged_once_as_an_error_with_the_exception()
    {
        var failure = new NoticeFailedException<TaskItem>(null, [new IOException("Changed failed.")]);
        var settings = new FakeSharedTaskSettings { SetFailure = failure };
        var setting = new DeletionDelaySetting(settings, _log.CreateLogger());
        setting.Refresh();

        setting.Commit("30");

        AssertErrorWith(failure);
        Assert.Single(_log.Events, entry => entry.Level == LogEventLevel.Error);
    }

    [Fact]
    public void A_deletion_delay_that_cannot_be_read_is_logged_as_an_error_with_the_exception()
    {
        var failure = new IOException("The settings file is locked.");

        new DeletionDelaySetting(new FakeSharedTaskSettings { ReadFailure = failure }, _log.CreateLogger()).Refresh();

        AssertErrorWith(failure);
    }
}
