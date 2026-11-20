using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// What the device controls do: generate, copy and open the key, rename the device, and accept or forget the host
/// key. Each reports a problem as one message under the controls.
/// </summary>
[Unit]
public class DeviceActionsTests : IDisposable
{
    private readonly FakeSyncService _sync = new();
    private readonly FakeSetupActions _actions = new();
    private readonly SyncUi _ui;
    private readonly DeviceActions _device;
    private int _changed;

    public DeviceActionsTests()
    {
        _ui = new SyncUi(_sync, _actions, post => post(), Serilog.Core.Logger.None);
        _device = new DeviceActions(_ui, () => _changed++);
    }

    public void Dispose() => _ui.Dispose();

    private FakeSyncSetup Setup => _sync.SetupControl;

    private static FakeSshHostKey HostKey(bool canAccept) =>
        new("self.example", 2222, "ssh-ed25519", "SHA256:test-key", canAccept);

    // The key actions.

    [Fact]
    public async Task Generating_a_key_asks_the_setup_once_and_shows_no_message()
    {
        await _device.GenerateKeyAsync();

        Assert.Equal(1, Setup.GenerateCount);
        Assert.NotNull(_sync.Setup.State.PublicKey);
        Assert.Null(_device.Message);
    }

    [Fact]
    public async Task A_failed_key_generation_shows_why()
    {
        Setup.Failure = new IOException("Keys can't be stored.");

        await _device.GenerateKeyAsync();

        Assert.Equal("Keys can't be stored.", _device.Message);
    }

    private static readonly Uri KeyPage = new("https://example.com/settings/keys");

    [Fact]
    public async Task Generating_a_key_then_copies_the_new_public_key_and_opens_no_page()
    {
        Setup.Publish(new FakeSyncSetupState { AddKeyPage = KeyPage });

        await _device.GenerateKeyAsync();

        Assert.Equal(_sync.Setup.State.PublicKey, _actions.CopiedPublicKey);
        Assert.NotNull(_actions.CopiedPublicKey);
        Assert.Equal(["CopyPublicKey"], _actions.Calls);
        Assert.Null(_actions.OpenedKeyPage);
    }

    [Fact]
    public async Task A_failed_key_generation_copies_nothing()
    {
        Setup.Failure = new IOException("Keys can't be stored.");

        await _device.GenerateKeyAsync();

        Assert.Empty(_actions.Calls);
        Assert.Null(_actions.CopiedPublicKey);
    }

    [Fact]
    public async Task A_key_that_was_made_but_cannot_be_copied_stays_and_the_message_says_why()
    {
        _actions.CopyFailure = new InvalidOperationException("Clipboard is busy.");

        await _device.GenerateKeyAsync();

        Assert.Equal("Clipboard is busy.", _device.Message);
        Assert.Equal(1, Setup.GenerateCount);
        Assert.NotNull(_sync.Setup.State.PublicKey);
        Assert.Empty(_actions.Calls);
    }

    [Fact]
    public void Copying_the_public_key_only_copies_it_and_opens_no_page()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key", AddKeyPage = KeyPage });

        _device.CopyPublicKey();

        Assert.Equal("ssh-rsa test-key", _actions.CopiedPublicKey);
        Assert.Equal(["CopyPublicKey"], _actions.Calls);
        Assert.Null(_actions.OpenedKeyPage);
        Assert.Null(_device.Message);
    }

    [Fact]
    public void Copying_asks_the_setup_to_check_no_address_and_changes_no_setup()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });

        _device.CopyPublicKey();

        Assert.Empty(Setup.AddressChecks);
        Assert.Equal(0, Setup.GenerateCount);
        Assert.Equal(0, Setup.ForgetKeyCount);
    }

    [Fact]
    public void Copying_before_a_key_exists_says_to_generate_one_first_and_copies_nothing()
    {
        _device.CopyPublicKey();

        Assert.Empty(_actions.Calls);
        Assert.Equal("Generate a key first.", _device.Message);
        Assert.Equal(0, Setup.GenerateCount);
    }

    [Fact]
    public void A_clipboard_action_that_fails_shows_why()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        _actions.CopyFailure = new InvalidOperationException("Clipboard is busy.");

        _device.CopyPublicKey();

        Assert.Equal("Clipboard is busy.", _device.Message);
        Assert.Empty(_actions.Calls);
    }

    [Fact]
    public void A_later_successful_copy_clears_the_message_of_a_failed_one()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        _actions.CopyFailure = new InvalidOperationException("Clipboard is busy.");
        _device.CopyPublicKey();
        _actions.CopyFailure = null;

        _device.CopyPublicKey();

        Assert.Null(_device.Message);
    }

    // Forget key.

    [Fact]
    public async Task Forgetting_the_key_asks_the_setup_once_and_the_public_key_goes()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });

        await _device.ForgetKeyAsync();

        Assert.Equal(1, Setup.ForgetKeyCount);
        Assert.Null(_sync.Setup.State.PublicKey);
        Assert.Null(_device.Message);
    }

    [Fact]
    public async Task Forgetting_the_key_never_accepts_a_host_key_and_leaves_the_clone_alone()
    {
        Setup.Publish(new FakeSyncSetupState
        {
            PublicKey = "ssh-rsa test-key", HostKey = HostKey(canAccept: false),
            RepositoryAddress = "git@example.com:tasks.git", RepositoryFolder = @"C:\Tasks",
        });

        await _device.ForgetKeyAsync();

        Assert.Null(Setup.AcceptedFingerprint);
        Assert.NotNull(_sync.Setup.State.HostKey);
        Assert.Equal("git@example.com:tasks.git", _sync.Setup.State.RepositoryAddress);
        Assert.Empty(Setup.Clones);
        Assert.Empty(Setup.Selections);
    }

    [Fact]
    public async Task Forgetting_the_key_copies_and_opens_nothing_and_generates_no_key()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key", AddKeyPage = KeyPage });

        await _device.ForgetKeyAsync();

        Assert.Empty(_actions.Calls);
        Assert.Equal(0, Setup.GenerateCount);
    }

    [Fact]
    public async Task A_failed_forget_key_shows_why_and_the_key_stays()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        Setup.Failure = new IOException("The key file is locked.");

        await _device.ForgetKeyAsync();

        Assert.Equal("The key file is locked.", _device.Message);
        Assert.Equal("ssh-rsa test-key", _sync.Setup.State.PublicKey);
        Assert.Equal(0, Setup.ForgetKeyCount);
    }

    [Fact]
    public async Task A_new_key_generated_after_forgetting_is_the_one_copied()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa old-key" });
        await _device.ForgetKeyAsync();

        await _device.GenerateKeyAsync();

        Assert.Equal(_sync.Setup.State.PublicKey, _actions.CopiedPublicKey);
        Assert.NotEqual("ssh-rsa old-key", _actions.CopiedPublicKey);
    }

    [Fact]
    public async Task Forgetting_the_key_tells_the_window_to_show_the_change()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        var before = _changed;

        await _device.ForgetKeyAsync();

        Assert.True(_changed > before);
    }

    // The device name.

    [Fact]
    public async Task A_new_device_name_is_saved_through_the_setup()
    {
        await _device.SetNameAsync("Work laptop");

        Assert.Equal("Work laptop", _sync.Setup.State.DeviceName);
        Assert.Null(_device.Message);
    }

    [Fact]
    public async Task The_device_name_is_trimmed_before_saving()
    {
        await _device.SetNameAsync("  Work laptop ");

        Assert.Equal("Work laptop", _sync.Setup.State.DeviceName);
    }

    [Theory]
    [InlineData("Test device")]
    [InlineData("  Test device  ")]
    public async Task An_unchanged_device_name_saves_nothing(string text)
    {
        var saves = 0;
        Setup.BeforeOperation = _ =>
        {
            saves++;
            return Task.CompletedTask;
        };

        await _device.SetNameAsync(text);

        Assert.Equal(0, saves);
    }

    [Fact]
    public async Task A_failed_name_save_shows_why_and_keeps_the_saved_name()
    {
        Setup.Failure = new IOException("Device storage is full.");

        await _device.SetNameAsync("Unsaved name");

        Assert.Equal("Device storage is full.", _device.Message);
        Assert.Equal("Test device", _sync.Setup.State.DeviceName);
    }

    [Fact]
    public async Task A_name_save_clears_an_earlier_message()
    {
        _device.CopyPublicKey();
        Assert.NotNull(_device.Message);

        await _device.SetNameAsync("Work laptop");

        Assert.Null(_device.Message);
    }

    // The host key.

    [Fact]
    public async Task Accepting_a_new_host_key_passes_that_exact_fingerprint()
    {
        Setup.Publish(new FakeSyncSetupState { HostKey = HostKey(canAccept: true) });

        await _device.AcceptHostKeyAsync();

        Assert.Equal("SHA256:test-key", Setup.AcceptedFingerprint);
    }

    [Fact]
    public async Task A_host_key_that_cannot_be_accepted_is_not_accepted()
    {
        Setup.Publish(new FakeSyncSetupState { HostKey = HostKey(canAccept: false) });

        await _device.AcceptHostKeyAsync();

        Assert.Null(Setup.AcceptedFingerprint);
    }

    [Fact]
    public async Task Accepting_with_no_host_key_does_nothing()
    {
        await _device.AcceptHostKeyAsync();

        Assert.Null(Setup.AcceptedFingerprint);
        Assert.Null(_device.Message);
    }

    [Fact]
    public async Task A_failed_accept_shows_why()
    {
        Setup.Publish(new FakeSyncSetupState { HostKey = HostKey(canAccept: true) });
        Setup.Failure = new IOException("Trust store is locked.");

        await _device.AcceptHostKeyAsync();

        Assert.Equal("Trust store is locked.", _device.Message);
    }

    [Fact]
    public void Tells_the_window_when_the_message_changes()
    {
        _device.CopyPublicKey();

        Assert.True(_changed > 0);
    }
}
