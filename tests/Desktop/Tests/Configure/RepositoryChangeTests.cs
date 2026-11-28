using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Changing the repository: choosing Clone or Use existing, the controls that choice shows, and the change itself.
/// The change hides the controls at once and shows them again, with why, when it fails.
/// </summary>
[Unit]
public class RepositoryChangeTests : IDisposable
{
    private const string Address = "git@example.com:tasks.git";
    private const string Folder = @"C:\New Tasks";

    private readonly FakeSyncService _sync = new();
    private readonly FakeSetupActions _actions = new();
    private readonly SyncUi _ui;
    private readonly RepositoryChange _change;
    private int _changed;

    public RepositoryChangeTests()
    {
        _ui = new SyncUi(_sync, _actions, post => post(), Serilog.Core.Logger.None);
        _change = new RepositoryChange(_ui, () => _changed++);
    }

    public void Dispose() => _ui.Dispose();

    private FakeSyncSetup Setup => _sync.SetupControl;

    // What it shows.

    [Fact]
    public void Shows_the_repository_lines_the_setup_holds()
    {
        Setup.Publish(new FakeSyncSetupState { RepositoryAddress = Address, RepositoryFolder = @"C:\Tasks", DeviceName = "Desk" });

        _change.Refresh();

        Assert.Equal(new RepositoryLines(Address, @"C:\Tasks", "Desk"), _change.Lines);
    }

    [Fact]
    public void Shows_None_when_the_device_has_no_clone()
    {
        _change.Refresh();

        Assert.Equal("None", _change.Lines.Address);
        Assert.Equal("None", _change.Lines.Folder);
    }

    [Fact]
    public void Starts_with_no_choice_and_no_controls()
    {
        Assert.Equal(RepositoryChoice.None, _change.Choice);
        Assert.False(_change.ControlsVisible);
        Assert.Null(_change.Message);
    }

    // Choosing.

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_choice_shows_its_controls(bool clone)
    {
        var choice = clone ? RepositoryChoice.Clone : RepositoryChoice.UseExisting;

        _change.Choose(choice);

        Assert.Equal(choice, _change.Choice);
        Assert.True(_change.ControlsVisible);
    }

    [Fact]
    public void The_other_choice_replaces_the_first()
    {
        _change.Choose(RepositoryChoice.Clone);

        _change.Choose(RepositoryChoice.UseExisting);

        Assert.Equal(RepositoryChoice.UseExisting, _change.Choice);
    }

    [Fact]
    public async Task A_choice_clears_the_message_of_an_earlier_attempt()
    {
        _change.Choose(RepositoryChoice.Clone);
        await _change.ChangeAsync("", "");
        Assert.NotNull(_change.Message);

        _change.Choose(RepositoryChoice.Clone);

        Assert.Null(_change.Message);
    }

    [Fact]
    public void A_choice_tells_the_window()
    {
        _change.Choose(RepositoryChoice.Clone);

        Assert.True(_changed > 0);
    }

    // Cloning and using an existing clone.

    [Fact]
    public async Task Clone_passes_the_address_and_folder_then_runs_one_sync_and_hides_the_controls()
    {
        _change.Choose(RepositoryChoice.Clone);

        await _change.ChangeAsync(Address, Folder);

        Assert.Equal([(Address, Folder)], Setup.Clones);
        Assert.Empty(Setup.Selections);
        Assert.Equal(1, _sync.SyncCount);
        Assert.False(_change.ControlsVisible);
        Assert.Null(_change.Message);
    }

    [Fact]
    public async Task Use_existing_passes_the_address_and_folder_then_runs_one_sync()
    {
        _change.Choose(RepositoryChoice.UseExisting);

        await _change.ChangeAsync(Address, Folder);

        Assert.Equal([(Address, Folder)], Setup.Selections);
        Assert.Empty(Setup.Clones);
        Assert.Equal(1, _sync.SyncCount);
    }

    [Fact]
    public async Task The_address_and_folder_are_trimmed()
    {
        _change.Choose(RepositoryChoice.Clone);

        await _change.ChangeAsync($"  {Address} ", $" {Folder}  ");

        Assert.Equal([(Address, Folder)], Setup.Clones);
    }

    [Theory]
    [InlineData("", Folder)]
    [InlineData(Address, "")]
    [InlineData("   ", Folder)]
    [InlineData(Address, "   ")]
    public async Task Missing_input_is_reported_and_nothing_is_changed(string address, string folder)
    {
        _change.Choose(RepositoryChoice.Clone);

        await _change.ChangeAsync(address, folder);

        Assert.Equal("Enter the repository's address and the local folder.", _change.Message);
        Assert.Empty(Setup.Clones);
        Assert.Equal(0, _sync.SyncCount);
        Assert.True(_change.ControlsVisible);
    }

    [Fact]
    public async Task A_pending_change_keeps_the_controls_hidden_until_it_finishes()
    {
        var pending = new TaskCompletionSource();
        Setup.BeforeOperation = _ => pending.Task;
        _change.Choose(RepositoryChoice.Clone);

        var running = _change.ChangeAsync(Address, Folder);

        Assert.False(_change.ControlsVisible);
        pending.SetResult();
        await running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_failed_change_shows_the_controls_again_with_the_error()
    {
        Setup.Failure = new IOException("Clone refused.");
        _change.Choose(RepositoryChoice.Clone);

        await _change.ChangeAsync(Address, Folder);

        Assert.Equal("Clone refused.", _change.Message);
        Assert.True(_change.ControlsVisible);
        Assert.Equal(0, _sync.SyncCount);
    }

    [Fact]
    public async Task A_failed_change_keeps_the_current_selection()
    {
        Setup.Publish(new FakeSyncSetupState { RepositoryFolder = @"C:\Old" });
        Setup.Failure = new IOException("Clone refused.");
        _change.Choose(RepositoryChoice.Clone);

        await _change.ChangeAsync(Address, Folder);
        _change.Refresh();

        Assert.Equal(@"C:\Old", _change.Lines.Folder);
    }

    [Theory]
    [InlineData(SyncState.Error, SyncFailure.Authentication)]
    [InlineData(SyncState.Offline, SyncFailure.Connection)]
    public async Task A_failed_first_sync_keeps_the_clone_and_shows_the_details(SyncState state, SyncFailure failure)
    {
        Setup.Publish(new FakeSyncSetupState { IsConfigured = false });
        _sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(
            new FakeSyncStatus { State = state, Failure = failure, Details = "Read-only key" });
        _change.Choose(RepositoryChoice.UseExisting);

        await _change.ChangeAsync(Address, Folder);
        _change.Refresh();

        Assert.Equal("Read-only key", _change.Message);
        Assert.Equal(Folder, _change.Lines.Folder);
        Assert.False(_sync.Setup.State.IsConfigured);
    }

    [Fact]
    public async Task A_new_attempt_clears_the_message_of_the_last_one()
    {
        Setup.Failure = new IOException("Clone refused.");
        _change.Choose(RepositoryChoice.Clone);
        await _change.ChangeAsync(Address, Folder);
        Setup.Failure = null;

        await _change.ChangeAsync(Address, Folder);

        Assert.Null(_change.Message);
    }

    // Set key: copies the public key, then opens the add-key page of the host of the address being typed.

    private const string GitHubAddress = "git@github.com:me/tasks.git";
    private static readonly Uri GitHubPage = new("https://github.com/me/tasks/settings/keys/new");
    private static readonly Uri ClonePage = new("https://clone.example/settings/keys");

    private void Accepts(string address, Uri? page) => Setup.AcceptedAddresses[address] = page;

    [Theory]
    [InlineData(GitHubAddress, true)]
    [InlineData("  git@github.com:me/tasks.git  ", true)]
    [InlineData("not an address", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void Set_key_is_available_only_while_the_typed_address_is_one_the_setup_accepts(string typed, bool expected)
    {
        Accepts(GitHubAddress, GitHubPage);

        Assert.Equal(expected, _change.CanSetKey(typed));
    }

    [Fact]
    public void Set_key_is_available_with_no_key_and_no_selected_clone()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = null, RepositoryAddress = null, RepositoryFolder = null });
        Accepts(GitHubAddress, GitHubPage);

        Assert.True(_change.CanSetKey(GitHubAddress));
    }

    [Fact]
    public void Set_key_follows_the_typed_address_not_the_selected_clones()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key", RepositoryAddress = "git@clone.example:me/tasks.git" });
        Accepts("git@clone.example:me/tasks.git", ClonePage);

        Assert.False(_change.CanSetKey("not an address"));
        Accepts(GitHubAddress, GitHubPage);
        Assert.True(_change.CanSetKey(GitHubAddress));
    }

    [Fact]
    public void Set_key_with_a_key_copies_it_then_opens_the_page_of_the_typed_addresss_host()
    {
        Setup.Publish(new FakeSyncSetupState
        {
            PublicKey = "ssh-rsa test-key", RepositoryAddress = "git@clone.example:me/tasks.git", AddKeyPage = ClonePage,
        });
        Accepts(GitHubAddress, GitHubPage);

        _change.SetKey(GitHubAddress);

        Assert.Equal("ssh-rsa test-key", _actions.CopiedPublicKey);
        Assert.Equal(GitHubPage, _actions.OpenedKeyPage);
        Assert.Equal(["CopyPublicKey", "OpenKeyPage"], _actions.Calls);
        Assert.Null(_change.Message);
    }

    [Fact]
    public void Set_key_asks_the_setup_about_the_typed_text()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        Accepts(GitHubAddress, GitHubPage);

        _change.SetKey(GitHubAddress);

        Assert.Contains(GitHubAddress, Setup.AddressChecks);
    }

    [Fact]
    public void Set_key_with_no_key_asks_to_generate_one_first_without_copying_opening_or_generating()
    {
        Accepts(GitHubAddress, GitHubPage);

        _change.SetKey(GitHubAddress);

        Assert.Equal("Generate a key first.", _change.Message);
        Assert.Empty(_actions.Calls);
        Assert.Equal(0, Setup.GenerateCount);
        Assert.Null(_sync.Setup.State.PublicKey);
    }

    [Fact]
    public void Set_key_on_a_valid_host_with_no_known_page_copies_the_key_and_explains_without_opening_a_page()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        Accepts("git@git.example.org:me/tasks.git", null);

        _change.SetKey("git@git.example.org:me/tasks.git");

        Assert.Equal("ssh-rsa test-key", _actions.CopiedPublicKey);
        Assert.Equal(["CopyPublicKey"], _actions.Calls);
        Assert.Equal("Copied. Add the key in your repository host's settings.", _change.Message);
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("")]
    [InlineData("C:\\tasks")]
    public void Set_key_with_an_address_the_setup_refuses_never_copies_or_opens(string typed)
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key", AddKeyPage = ClonePage });
        Accepts(GitHubAddress, GitHubPage);

        _change.SetKey(typed);

        Assert.Empty(_actions.Calls);
        Assert.Null(_actions.CopiedPublicKey);
        Assert.Null(_actions.OpenedKeyPage);
    }

    [Fact]
    public void A_clipboard_failure_in_Set_key_shows_why_and_opens_no_page()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        Accepts(GitHubAddress, GitHubPage);
        _actions.CopyFailure = new InvalidOperationException("Clipboard is busy.");

        _change.SetKey(GitHubAddress);

        Assert.Equal("Clipboard is busy.", _change.Message);
        Assert.Null(_actions.OpenedKeyPage);
        Assert.Empty(_actions.Calls);
    }

    [Fact]
    public void A_browser_failure_in_Set_key_shows_why_and_the_key_stays_copied()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });
        Accepts(GitHubAddress, GitHubPage);
        _actions.OpenFailure = new InvalidOperationException("No browser.");

        _change.SetKey(GitHubAddress);

        Assert.Equal("No browser.", _change.Message);
        Assert.Equal("ssh-rsa test-key", _actions.CopiedPublicKey);
    }

    [Fact]
    public void A_later_successful_Set_key_clears_the_message_of_an_earlier_one()
    {
        Accepts(GitHubAddress, GitHubPage);
        _change.SetKey(GitHubAddress);
        Assert.NotNull(_change.Message);
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key" });

        _change.SetKey(GitHubAddress);

        Assert.Null(_change.Message);
    }

    [Fact]
    public void Set_key_never_clones_selects_syncs_or_changes_the_repository_choice()
    {
        Setup.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key", RepositoryAddress = "git@clone.example:me/tasks.git" });
        Accepts(GitHubAddress, GitHubPage);
        _change.Choose(RepositoryChoice.Clone);

        _change.SetKey(GitHubAddress);

        Assert.Empty(Setup.Clones);
        Assert.Empty(Setup.Selections);
        Assert.Equal(0, _sync.SyncCount);
        Assert.Equal("git@clone.example:me/tasks.git", _sync.Setup.State.RepositoryAddress);
        Assert.Equal(RepositoryChoice.Clone, _change.Choice);
        Assert.True(_change.ControlsVisible);
    }

    [Fact]
    public void Set_key_tells_the_window_when_its_message_changes()
    {
        Accepts(GitHubAddress, GitHubPage);
        var before = _changed;

        _change.SetKey(GitHubAddress);

        Assert.True(_changed > before);
    }

    // The bug: the design says a failure to read the lines shows as the section's message.

    [Fact]
    public void A_failure_to_read_the_setup_shows_as_the_message()
    {
        var unreadable = new ThrowingSetupSync(new InvalidDataException("Setup file is damaged."));
        using var ui = new SyncUi(unreadable, new FakeSetupActions(), post => post(), Serilog.Core.Logger.None);
        var change = new RepositoryChange(ui, () => { });

        var failure = Record.Exception(change.Refresh);

        Assert.Null(failure);
        Assert.Contains("Setup file is damaged.", change.Message);
    }

    /// <summary>A sync service whose setup state can't be read.</summary>
    private sealed class ThrowingSetupSync(Exception failure) : ISyncService
    {
        private readonly FakeSyncService _inner = new();

        public ISyncStatus Status => _inner.Status;

        public ISyncSetup Setup { get; } = new ThrowingSetup(failure);

        public event EventHandler? StatusChanged
        {
            add { }
            remove { }
        }

        public void Start() => _inner.Start();

        public Task<ISyncStatus> SyncNowAsync(CancellationToken cancellationToken) => _inner.SyncNowAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => _inner.StopAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class ThrowingSetup(Exception failure) : ISyncSetup
    {
        public ISyncSetupState State => throw failure;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task SetDeviceNameAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task GenerateKeyAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CloneAsync(string address, string folder, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UseExistingAsync(string address, string folder, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AcceptHostKeyAsync(string fingerprint, CancellationToken cancellationToken) => Task.CompletedTask;


        public Task ForgetKeyAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool CheckAddress(string address, out Uri? addKeyPage)
        {
            addKeyPage = null;
            return false;
        }
    }
}
