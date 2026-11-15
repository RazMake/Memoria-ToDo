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
        _ui = new SyncUi(_sync, _actions, post => post());
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

    [Fact]
    public void Copying_the_public_key_hands_it_to_the_clipboard_action()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });

        _device.CopyPublicKey();

        Assert.Equal("ssh-rsa test-key", _actions.CopiedPublicKey);
        Assert.Null(_device.Message);
    }

    [Fact]
    public void Copying_before_a_key_exists_says_to_generate_one_first()
    {
        _device.CopyPublicKey();

        Assert.Null(_actions.CopiedPublicKey);
        Assert.Equal("Generate a key first.", _device.Message);
    }

    [Fact]
    public void A_clipboard_action_that_fails_shows_why()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        _actions.Failure = new InvalidOperationException("Clipboard is busy.");

        _device.CopyPublicKey();

        Assert.Equal("Clipboard is busy.", _device.Message);
    }

    [Fact]
    public void A_successful_copy_clears_an_earlier_message()
    {
        _device.CopyPublicKey();
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });

        _device.CopyPublicKey();

        Assert.Null(_device.Message);
    }

    [Fact]
    public void Opening_the_key_page_hands_the_hosts_address_to_the_browser_action()
    {
        var page = new Uri("https://example.com/settings/keys");
        Setup.Publish(new FakeSyncSetupState { AddKeyPage = page });

        _device.OpenKeyPage();

        Assert.Equal(page, _actions.OpenedKeyPage);
        Assert.Null(_device.Message);
    }

    [Fact]
    public void Opening_the_key_page_of_a_host_that_has_none_says_so()
    {
        _device.OpenKeyPage();

        Assert.Null(_actions.OpenedKeyPage);
        Assert.Equal("The repository's host has no page to add a key.", _device.Message);
    }

    [Fact]
    public void A_browser_action_that_fails_shows_why()
    {
        Setup.Publish(new FakeSyncSetupState { AddKeyPage = new Uri("https://example.com/keys") });
        _actions.Failure = new InvalidOperationException("No browser.");

        _device.OpenKeyPage();

        Assert.Equal("No browser.", _device.Message);
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
    public async Task A_changed_host_key_cannot_be_accepted()
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
    public async Task Forgetting_a_host_key_passes_its_host_and_port()
    {
        Setup.Publish(new FakeSyncSetupState { HostKey = HostKey(canAccept: false) });

        await _device.ForgetHostKeyAsync();

        Assert.Equal(("self.example", 2222), Setup.ForgottenHost);
    }

    [Fact]
    public async Task Forgetting_with_no_host_key_does_nothing()
    {
        await _device.ForgetHostKeyAsync();

        Assert.Null(Setup.ForgottenHost);
    }

    [Fact]
    public async Task A_failed_forget_shows_why()
    {
        Setup.Publish(new FakeSyncSetupState { HostKey = HostKey(canAccept: false) });
        Setup.Failure = new IOException("Trust store is locked.");

        await _device.ForgetHostKeyAsync();

        Assert.Equal("Trust store is locked.", _device.Message);
    }

    [Fact]
    public void Tells_the_window_when_the_message_changes()
    {
        _device.CopyPublicKey();

        Assert.True(_changed > 0);
    }
}
