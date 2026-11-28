using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests.Configure;

/// <summary>
/// The Configure screen's Behavior and Sync settings paths: the Start with Windows box, the repository, device and status
/// controls, and the Git Access keys. Each test follows one whole flow. What each action does and how it fails are
/// unit-tested on StartWithWindowsRow, DeviceActions, RepositoryChange, SyncUi and DeletionDelaySetting.
/// The Forget key dialog is modal, so <see cref="ClosePrompt"/> answers it while the click waits.
/// </summary>
[UX("Configure")]
[Collection(ScreenTests.Collection)]
public class ConfigureSyncPathTests
{
    private const string KeyLine = "ssh-rsa test-key Desk";
    private const string GitHubAddress = "git@github.com:me/tasks.git";
    private static readonly Uri GitHubPage = new("https://github.com/me/tasks/settings/keys/new");

    private const string ForgetText =
        "Delete this device's SSH key? Sync stops until you generate a new key and add it at your repository's host. "
        + "The old key stays listed at the host until you remove it there.";

    // Long enough to wrap to a second line, so it would widen a window that fits its content.
    private const string TwoLineError =
        "Windows didn't let the app change its startup setting. Check whether a policy or another app manages startup apps, then try again.";

    private readonly ClosePrompt _prompt = new("Forget key");

    [WpfFact]
    public void The_Behavior_section_lists_Start_with_Windows_Prevent_screen_saver_and_AI_assistants_where_a_failed_Start_with_Windows_switch_shows_its_error_and_puts_the_box_back_and_Examples_opens_with_AI_assistants_off()
    {
        var port = new FakeStartWithWindows { On = false, SetFailure = new InvalidOperationException(TwoLineError) };
        using var screen = new ConfigureScreen(port);
        screen.McpServer.Connections = [new("VS Code", "vs code lines")];
        screen.Open();
        screen.ClickTitle("Behavior");
        Assert.Equal(["Behavior"], screen.OpenSections);
        Assert.True(screen.KeepAwakeBox.IsVisible, "Prevent screen saver isn't in the Behavior section.");
        Assert.True(screen.AiBox.IsVisible, "The AI assistants row isn't in the Behavior section.");
        double Top(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), screen.Window).Y;
        Assert.True(Top(screen.StartWithWindowsBox) < Top(screen.KeepAwakeBox));
        Assert.True(Top(screen.KeepAwakeBox) < Top(screen.AiBox));
        Assert.False(screen.StartWithWindowsBox.IsChecked);
        var size = screen.WindowSize;

        screen.SwitchStartWithWindows();

        Assert.Equal([true], port.SetCalls);
        Assert.True(screen.ShowsText(TwoLineError));
        Assert.False(screen.StartWithWindowsBox.IsChecked);
        Assert.Equal(size, screen.WindowSize);
        Assert.Contains(screen.Log.Events, entry => entry.Level == LogEventLevel.Error);

        port.SetFailure = null;
        screen.SwitchStartWithWindows();

        Assert.Equal([true, true], port.SetCalls);
        Assert.True(screen.StartWithWindowsBox.IsChecked);

        screen.SwitchKeepAwake();
        Assert.Equal([true], screen.KeepAwake.SetOnCalls);
        Assert.True(screen.KeepAwakeBox.IsChecked);

        Assert.True(screen.AiBox.IsChecked);
        screen.SwitchAi();
        Assert.Equal([false], screen.McpServer.SetOnCalls);
        Assert.False(screen.AiBox.IsChecked);
        var opened = false;
        screen.OpenExamples(dialog =>
        {
            opened = true;
            Assert.Same(screen.Window, dialog.Owner);
            OpenWindows.Click(dialog, "Close");
        });
        Assert.True(opened);
    }

    [WpfFact]
    public async Task Sync_settings_shows_the_repository_and_device_clones_or_uses_a_repository_syncs_and_follows_notices_and_saves_the_device_name_and_deletion_delay()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState
        {
            RepositoryFolder = @"C:\Tasks", RepositoryAddress = "git@example.com:tasks.git", DeviceName = "Desk",
        });
        using var screen = Opened(sync);
        string[] cards = ["Git Access", "Repository", "Auto"];

        Assert.Equal(cards, screen.Texts.Where(text => cards.Contains(text)));
        Assert.True(screen.ShowsText("Repository: git@example.com:tasks.git"));
        Assert.True(screen.ShowsText(@"Local Path: C:\Tasks"));
        Assert.True(screen.ShowsText("Identity: Desk"));

        screen.ChooseRepositoryAction("Use existing repo clone");
        Assert.True(VisibleButton(screen, "Use this clone"));
        Assert.False(VisibleButton(screen, "Clone"));
        screen.Box("Repository address").Text = "git@example.com:tasks.git";
        screen.Box("Local folder").Text = @"C:\Existing";
        screen.Click("Use this clone");

        Assert.Equal([("git@example.com:tasks.git", @"C:\Existing")], sync.SetupControl.Selections);
        Assert.Equal(1, sync.SyncCount);
        Assert.True(screen.ShowsText(@"Local Path: C:\Existing"));

        screen.ChooseRepositoryAction("Clone a repo");
        Assert.True(VisibleButton(screen, "Clone"));
        Assert.False(VisibleButton(screen, "Use this clone"));
        screen.Box("Repository address").Text = "git@example.com:other.git";
        screen.Box("Local folder").Text = @"C:\New Tasks";
        screen.Click("Clone");

        Assert.Equal([("git@example.com:other.git", @"C:\New Tasks")], sync.SetupControl.Clones);
        Assert.Equal(2, sync.SyncCount);
        Assert.True(screen.ShowsText(@"Local Path: C:\New Tasks"));
        Assert.False(VisibleButton(screen, "Clone"));

        screen.Click("Sync");
        Assert.Equal(3, sync.SyncCount);

        await Task.Run(() =>
        {
            sync.SetupControl.Publish(new FakeSyncSetupState { DeviceName = "Updated elsewhere" });
            sync.Publish(new FakeSyncStatus { State = SyncState.Error, Failure = SyncFailure.Authentication, Details = "Key rejected" });
        });
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.True(screen.ShowsText("Identity: Updated elsewhere"));
        Assert.True(screen.ShowsText("Key rejected"));

        var delay = screen.Box("Deletion delay (days)");
        ConfigureScreen.Focus(delay);
        delay.Text = "30";
        var name = screen.Box("Device name");
        ConfigureScreen.Focus(name);
        name.Text = "Work laptop";
        screen.FocusStartWithWindowsBox();

        Assert.Equal(30, screen.Store.Settings.DeletionDelayDays);
        Assert.Equal("Work laptop", sync.Setup.State.DeviceName);
        Assert.True(screen.ShowsText("Identity: Work laptop"));
        screen.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public void Git_Access_generates_copies_and_forgets_the_key_after_asking_and_Set_key_copies_it_and_opens_the_hosts_page()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState
        {
            PublicKey = null, DeviceName = "Desk", HostKey = new FakeSshHostKey("self.example", 2222, "ssh-ed25519", "SHA256:test-key", true),
        });
        sync.SetupControl.AcceptedAddresses[GitHubAddress] = GitHubPage;
        using var screen = Opened(sync);

        Assert.True(screen.ShowsButton("Generate key"));
        Assert.False(screen.ShowsButton("Copy public key"));
        Assert.False(screen.ShowsButton("Forget key"));
        Assert.True(screen.IsInCard(screen.ButtonNamed("Accept host key"), "Git Access"));
        Assert.False(screen.ShowsButton("Forget host key"));
        Assert.DoesNotContain(screen.Texts, text => text.Contains("self.example", StringComparison.Ordinal) || text.Contains("SHA256:", StringComparison.Ordinal));

        screen.Click("Generate key");

        Assert.Equal(1, sync.SetupControl.GenerateCount);
        Assert.Equal(sync.Setup.State.PublicKey, screen.SetupActions.CopiedPublicKey);
        Assert.Equal(["CopyPublicKey"], screen.SetupActions.Calls);
        Assert.Null(screen.SetupActions.OpenedKeyPage);
        Assert.False(screen.ShowsButton("Generate key"));
        Assert.True(screen.ShowsButton("Copy public key"));
        Assert.True(screen.ShowsButton("Forget key"));

        var copyFailure = new InvalidOperationException("Clipboard is busy.");
        screen.SetupActions.CopyFailure = copyFailure;
        screen.Click("Copy public key");
        Assert.True(screen.IsInCard(screen.TitleBlock("Clipboard is busy."), "Git Access"));
        screen.SetupActions.CopyFailure = null;

        ClickForget(screen, ClosePrompt.Click("Cancel"));

        Assert.Equal(1, _prompt.Shown);
        Assert.Same(screen.Window, _prompt.Owner);
        Assert.True(_prompt.Centered);
        Assert.Contains(ForgetText, _prompt.Texts);
        Assert.Equal(0, sync.SetupControl.ForgetKeyCount);
        Assert.True(screen.ShowsButton("Forget key"));

        ClickForget(screen, ClosePrompt.Click("Forget key"));

        Assert.Equal(1, sync.SetupControl.ForgetKeyCount);
        Assert.Null(sync.Setup.State.PublicKey);
        Assert.True(screen.ShowsButton("Generate key"));
        Assert.False(screen.ShowsButton("Copy public key"));
        Assert.Null(sync.SetupControl.AcceptedFingerprint);
        screen.Click("Accept host key");
        Assert.Equal("SHA256:test-key", sync.SetupControl.AcceptedFingerprint);

        Assert.False(screen.ShowsButton("Set key"));
        screen.ChooseRepositoryAction("Clone a repo");
        Assert.True(screen.ShowsButton("Set key"));
        Assert.True(screen.IsInCard(screen.ButtonNamed("Set key"), "Repository"));
        var address = screen.Box("Repository address");
        Assert.False(screen.ButtonNamed("Set key").IsEnabled);
        address.Text = GitHubAddress;
        Assert.True(screen.ButtonNamed("Set key").IsEnabled);

        var calls = screen.SetupActions.Calls.Count;
        screen.Click("Set key");
        Assert.True(screen.IsInCard(screen.TitleBlock("Generate a key first."), "Repository"));
        Assert.Equal(calls, screen.SetupActions.Calls.Count);

        screen.Click("Generate key");
        calls = screen.SetupActions.Calls.Count;
        screen.Click("Set key");

        Assert.Equal(sync.Setup.State.PublicKey, screen.SetupActions.CopiedPublicKey);
        Assert.Equal(GitHubPage, screen.SetupActions.OpenedKeyPage);
        Assert.Equal(["CopyPublicKey", "OpenKeyPage"], screen.SetupActions.Calls.Skip(calls));
        Assert.Empty(sync.SetupControl.Clones);
        Assert.Equal(GitHubAddress, screen.Box("Repository address").Text);

        screen.ChooseRepositoryAction("Use existing repo clone");
        Assert.False(screen.ShowsButton("Set key"));
        var loggedFailure = screen.Log.Events.SingleAt(LogEventLevel.Error);
        Assert.True(loggedFailure.HasException(copyFailure));
        screen.Log.Events.Where(entry => !ReferenceEquals(entry, loggedFailure)).AssertNoProblems();
    }

    private void ClickForget(ConfigureScreen screen, Action<Window> answer) =>
        _prompt.Run(() =>
        {
            screen.Click("Forget key");
            return 0;
        }, answer);

    private static ConfigureScreen Opened(FakeSyncService sync)
    {
        var screen = new ConfigureScreen(sync: sync);
        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.Window.UpdateLayout();
        return screen;
    }

    private static bool VisibleButton(ConfigureScreen screen, string text) =>
        OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == text).IsVisible;
}
