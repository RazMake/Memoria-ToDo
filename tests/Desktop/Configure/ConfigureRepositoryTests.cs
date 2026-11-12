using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

[UX]
public class ConfigureRepositoryTests
{
    [WpfFact]
    public void Shows_none_for_the_repository_and_local_path_when_the_device_has_no_clone()
    {
        using var screen = Opened();

        Assert.True(screen.ShowsText("Repository: None"));
        Assert.True(screen.ShowsText("Local Path: None"));
    }

    [WpfFact]
    public void Shows_the_selected_clone_address_and_device_identity()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState
        {
            RepositoryFolder = @"C:\Tasks", RepositoryAddress = "git@example.com:tasks.git", DeviceName = "Desk"
        });
        using var screen = Opened(sync);

        Assert.True(screen.ShowsText("Repository: git@example.com:tasks.git"));
        Assert.True(screen.ShowsText(@"Local Path: C:\Tasks"));
        Assert.True(screen.ShowsText("Identity: Desk"));
    }

    [WpfTheory]
    [InlineData("Repository:")]
    [InlineData("Local Path:")]
    [InlineData("Identity:")]
    public void Shows_the_field_name_in_bold(string name)
    {
        using var screen = Opened();

        var line = OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .Single(b => OpenWindows.TextOf(b).StartsWith(name, StringComparison.Ordinal));
        var first = Assert.IsType<System.Windows.Documents.Run>(line.Inlines.FirstInline, exactMatch: false);
        Assert.Equal(name, first.Text);
        Assert.Equal(FontWeights.Bold, first.FontWeight);
    }

    [WpfFact]
    public void Clone_and_existing_clone_choices_replace_each_others_controls()
    {
        using var screen = Opened();
        screen.ChooseRepositoryAction("Clone a repo");
        Assert.True(VisibleButton(screen, "Clone"));
        Assert.False(VisibleButton(screen, "Use this clone"));

        screen.ChooseRepositoryAction("Use existing repo clone");

        Assert.True(VisibleButton(screen, "Use this clone"));
        Assert.False(VisibleButton(screen, "Clone"));
    }

    [WpfFact]
    public void Clone_uses_the_SSH_address_and_folder_then_checks_writable_sync()
    {
        using var screen = Opened();
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = "git@example.com:tasks.git";
        screen.Box("Local folder").Text = @"C:\New Tasks";

        screen.Click("Clone");

        Assert.Equal([("git@example.com:tasks.git", @"C:\New Tasks")], screen.Sync.SetupControl.Clones);
        Assert.Equal(1, screen.Sync.SyncCount);
        Assert.True(screen.ShowsText(@"Local Path: C:\New Tasks"));
        Assert.False(VisibleButton(screen, "Clone"));
    }

    [WpfFact]
    public void A_pending_clone_hides_its_controls_until_it_finishes()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sync = new FakeSyncService();
        sync.SetupControl.BeforeOperation = _ => pending.Task;
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = "git@example.com:tasks.git";
        screen.Box("Local folder").Text = @"C:\New";

        screen.Click("Clone");

        Assert.False(VisibleButton(screen, "Clone"));
        pending.SetResult();
    }

    [WpfFact]
    public void Missing_clone_input_is_reported_without_selecting_anything()
    {
        using var screen = Opened();
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = "git@example.com:tasks.git";

        screen.Click("Clone");

        Assert.Empty(screen.Sync.SetupControl.Clones);
        Assert.Equal(0, screen.Sync.SyncCount);
        Assert.True(screen.ShowsText("folder"));
        Assert.True(VisibleButton(screen, "Clone"));
    }

    [WpfFact]
    public void A_failed_clone_keeps_the_current_selection_and_restores_its_controls()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState { RepositoryFolder = @"C:\Old" });
        sync.SetupControl.Failure = new IOException("Clone refused.");
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = "git@example.com:tasks.git";
        screen.Box("Local folder").Text = @"C:\New";

        screen.Click("Clone");

        Assert.True(screen.ShowsText("Clone refused."));
        Assert.True(screen.ShowsText(@"Local Path: C:\Old"));
        Assert.True(VisibleButton(screen, "Clone"));
    }

    [WpfFact]
    public void An_existing_clone_is_selected_through_Sync_and_checked_for_writable_access()
    {
        using var screen = Opened();
        screen.ChooseRepositoryAction("Use existing repo clone");
        screen.Box("Repository address").Text = "git@example.com:tasks.git";
        screen.Box("Local folder").Text = @"C:\Existing";

        screen.Click("Use this clone");

        Assert.Equal([("git@example.com:tasks.git", @"C:\Existing")], screen.Sync.SetupControl.Selections);
        Assert.Equal(1, screen.Sync.SyncCount);
        Assert.True(screen.ShowsText(@"Local Path: C:\Existing"));
    }

    [WpfTheory]
    [InlineData(SyncState.Error, SyncFailure.Authentication)]
    [InlineData(SyncState.Offline, SyncFailure.Connection)]
    public void A_failed_first_sync_keeps_the_clone_but_reports_that_setup_is_not_complete(
        SyncState state, SyncFailure failure)
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = false });
        sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus
        {
            State = state, Failure = failure, Details = "Read-only key"
        });
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Use existing repo clone");
        screen.Box("Repository address").Text = "git@example.com:tasks.git";
        screen.Box("Local folder").Text = @"C:\Existing";

        screen.Click("Use this clone");

        Assert.True(screen.ShowsText(@"Local Path: C:\Existing"));
        Assert.True(screen.ShowsText("Read-only key"));
        Assert.False(sync.Setup.State.IsConfigured);
    }

    [WpfFact]
    public void Key_actions_generate_locally_copy_the_public_key_and_open_the_hosts_page()
    {
        var sync = new FakeSyncService();
        var page = new Uri("https://example.com/settings/keys");
        sync.SetupControl.Publish(new FakeSyncSetupState { AddKeyPage = page });
        using var screen = Opened(sync);

        screen.Click("Generate key");
        screen.Click("Copy public key");
        screen.Click("Add key at host");

        Assert.Equal(1, sync.SetupControl.GenerateCount);
        Assert.Equal(sync.Setup.State.PublicKey, screen.SetupActions.CopiedPublicKey);
        Assert.Equal(page, screen.SetupActions.OpenedKeyPage);
    }

    [WpfFact]
    public async Task Setup_and_status_notices_from_a_worker_thread_update_the_open_window()
    {
        using var screen = Opened();

        await Task.Run(() =>
        {
            screen.Sync.SetupControl.Publish(new FakeSyncSetupState { DeviceName = "Updated elsewhere" });
            screen.Sync.Publish(new FakeSyncStatus
            {
                State = SyncState.Error, Failure = SyncFailure.Authentication, Details = "Key rejected"
            });
        });
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.True(screen.ShowsText("Identity: Updated elsewhere"));
        Assert.True(screen.ShowsText("Key rejected"));
    }

    [WpfFact]
    public void An_unknown_custom_host_shows_its_key_and_accepts_that_exact_fingerprint()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState
        {
            HostKey = new FakeSshHostKey("self.example", 2222, "ssh-ed25519", "SHA256:unknown-test-key", true)
        });
        using var screen = Opened(sync);

        Assert.True(screen.ShowsText("self.example:2222"));
        Assert.True(screen.ShowsText("ssh-ed25519"));
        Assert.True(screen.ShowsText("SHA256:unknown-test-key"));
        screen.Click("Accept host key");

        Assert.Equal("SHA256:unknown-test-key", sync.SetupControl.AcceptedFingerprint);
    }

    [WpfFact]
    public void A_changed_trusted_key_cannot_be_accepted()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState
        {
            HostKey = new FakeSshHostKey("self.example", 2222, "ssh-ed25519", "SHA256:changed-test-key", false)
        });
        using var screen = Opened(sync);

        Assert.True(screen.ShowsText("SHA256:changed-test-key"));
        Assert.DoesNotContain(OpenWindows.Descendants(screen.Window).OfType<Button>(),
            button => button.Content as string == "Accept host key" && button.IsVisible && button.IsEnabled);
        Assert.Null(sync.SetupControl.AcceptedFingerprint);
    }

    [WpfFact]
    public void Forgetting_a_custom_host_passes_its_host_and_port()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState
        {
            HostKey = new FakeSshHostKey("self.example", 2222, "ssh-ed25519", "SHA256:old-test-key", false)
        });
        using var screen = Opened(sync);

        screen.Click("Forget host key");

        Assert.Equal(("self.example", 2222), sync.SetupControl.ForgottenHost);
    }

    [WpfFact]
    public void Changing_the_device_name_saves_when_focus_leaves_the_box()
    {
        using var screen = Opened();
        var name = screen.Box("Device name");
        ConfigureScreen.Focus(name);
        name.Text = "Work laptop";

        screen.FocusStartWithWindowsBox();

        Assert.Equal("Work laptop", screen.Sync.Setup.State.DeviceName);
        Assert.True(screen.ShowsText("Identity: Work laptop"));
    }

    [WpfFact]
    public void A_device_name_save_failure_restores_the_saved_name_and_shows_why()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Failure = new IOException("Device storage is full.");
        using var screen = Opened(sync);
        var name = screen.Box("Device name");
        ConfigureScreen.Focus(name);
        name.Text = "Unsaved name";

        screen.FocusStartWithWindowsBox();

        Assert.Equal("Test device", name.Text);
        Assert.True(screen.ShowsText("Device storage is full."));
    }

    [WpfFact]
    public void Sync_status_shows_the_last_success_pending_changes_and_skipped_files()
    {
        var sync = new FakeSyncService();
        sync.Publish(new FakeSyncStatus
        {
            LastSuccessfulSync = new DateTimeOffset(2038, 2, 1, 12, 0, 0, TimeSpan.Zero),
            HasPendingChanges = true,
            SkippedFiles = [new FakeSkippedFile("tasks/newer.json", "Newer task format", true)]
        });
        using var screen = Opened(sync);

        Assert.True(screen.ShowsText("2038"));
        Assert.True(screen.ShowsText("Changes wait to be synced."));
        Assert.True(screen.ShowsText("tasks/newer.json"));
        Assert.True(screen.ShowsText("Newer task format"));
    }

    [WpfFact]
    public void An_offline_status_shows_its_failure_details()
    {
        var sync = new FakeSyncService();
        sync.Publish(new FakeSyncStatus
        {
            State = SyncState.Offline, Failure = SyncFailure.Connection, Details = "Host unreachable"
        });
        using var screen = Opened(sync);

        Assert.True(screen.ShowsText("Host unreachable"));
    }

    [WpfFact]
    public void Sync_now_requests_a_pass()
    {
        using var screen = Opened();

        screen.Click("Sync now");

        Assert.Equal(1, screen.Sync.SyncCount);
    }

    private static ConfigureScreen Opened(FakeSyncService? sync = null)
    {
        var screen = new ConfigureScreen(sync: sync);
        screen.Open();
        screen.Expand("Sync settings");
        screen.Window.UpdateLayout();
        return screen;
    }

    private static bool VisibleButton(ConfigureScreen screen, string text) =>
        OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == text).IsVisible;
}
