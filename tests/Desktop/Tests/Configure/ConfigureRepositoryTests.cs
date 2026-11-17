using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The repository, device and status controls of Sync settings on screen: the common paths and the wiring. The
/// rules and failures are unit-tested on RepositoryChange, DeviceActions, SyncText and SyncUi.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureRepositoryTests
{
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
    public void An_unknown_custom_host_shows_no_host_details_and_accepts_that_exact_fingerprint()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState
        {
            HostKey = new FakeSshHostKey("self.example", 2222, "ssh-ed25519", "SHA256:unknown-test-key", true)
        });
        using var screen = Opened(sync);

        Assert.DoesNotContain(screen.Texts, text => text.Contains("self.example", StringComparison.Ordinal));
        Assert.DoesNotContain(screen.Texts, text => text.Contains("SHA256:", StringComparison.Ordinal));
        screen.Click("Accept host key");

        Assert.Equal("SHA256:unknown-test-key", sync.SetupControl.AcceptedFingerprint);
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
    }

    [WpfFact]
    public void The_Sync_button_requests_a_pass()
    {
        using var screen = Opened();

        screen.Click("Sync");

        Assert.Equal(1, screen.Sync.SyncCount);
    }

    [WpfFact]
    public void Shows_no_Sync_now_button()
    {
        using var screen = Opened();

        Assert.DoesNotContain(
            OpenWindows.Descendants(screen.Window).OfType<Button>(), button => button.Content as string == "Sync now");
    }

    [WpfFact]
    public void Sync_settings_shows_the_cards_Git_Access_Repository_and_Auto_in_order_and_no_other_titles()
    {
        using var screen = Opened();
        string[] cards = ["Git Access", "Repository", "Auto"];
        string[] oldTitles = ["Device", "Status", "Tasks"];

        Assert.Equal(cards, screen.Texts.Where(text => cards.Contains(text)));
        Assert.All(oldTitles, title => Assert.DoesNotContain(title, screen.Texts));
        var tops = cards.Select(card => Bounds(screen.TitleBlock(card), screen.Window).Top).ToList();
        Assert.True(tops[0] < tops[1] && tops[1] < tops[2], "The cards run top to bottom: Git Access, Repository, Auto.");
    }

    [WpfFact]
    public void The_Sync_button_starts_the_first_status_line_beside_the_last_sync_time()
    {
        using var screen = Opened();
        var button = OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == "Sync");
        var line = OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .First(b => OpenWindows.TextOf(b).StartsWith("Not synced yet.", StringComparison.Ordinal));

        var buttonBox = Bounds(button, screen.Window);
        var lineBox = Bounds(line, screen.Window);

        Assert.True(buttonBox.Right <= lineBox.Left + 1, "The button comes first, then the text.");
        Assert.True(buttonBox.Top < lineBox.Bottom && lineBox.Top < buttonBox.Bottom, "They share one line.");
    }

    [WpfFact]
    public void The_status_follows_the_three_repository_lines_and_stays_in_the_Repository_card()
    {
        using var screen = Opened();
        var identity = OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .Single(b => OpenWindows.TextOf(b).StartsWith("Identity:", StringComparison.Ordinal));
        var button = OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == "Sync");
        var auto = OpenWindows.Descendants(screen.Window).OfType<TextBlock>()
            .Single(b => OpenWindows.TextOf(b) == "Auto");

        Assert.True(Bounds(identity, screen.Window).Bottom <= Bounds(button, screen.Window).Top + 1);
        Assert.True(Bounds(button, screen.Window).Bottom <= Bounds(auto, screen.Window).Top + 1);
    }

    [WpfFact]
    public void Auto_shows_the_deletion_delay_as_one_bulleted_sentence()
    {
        using var screen = Opened();
        var window = screen.Window;
        var texts = OpenWindows.Descendants(window).OfType<TextBlock>().ToList();
        var bullet = Bounds(texts.Single(b => OpenWindows.TextOf(b) == "•"), window);
        var label = Bounds(texts.Single(b => OpenWindows.TextOf(b) == "Delete resolved tasks after"), window);
        var box = Bounds(screen.Box("Deletion delay (days)"), window);
        var days = Bounds(texts.Single(b => OpenWindows.TextOf(b) == "days"), window);

        Assert.True(bullet.Right <= label.Left + 1);
        Assert.True(label.Right <= box.Left + 1);
        Assert.True(box.Right <= days.Left + 1);
        Assert.True(box.Top < label.Bottom && label.Top < box.Bottom, "The sentence is on one line.");
    }

    [WpfFact]
    public void Shows_no_Add_key_at_host_button()
    {
        using var screen = Opened();

        Assert.DoesNotContain(
            OpenWindows.Descendants(screen.Window).OfType<Button>(), button => button.Content as string == "Add key at host");
    }

    [WpfFact]
    public void A_section_taller_than_the_window_scrolls_to_its_last_control()
    {
        var sync = new FakeSyncService();
        sync.Publish(new FakeSyncStatus
        {
            SkippedFiles = [.. Enumerable.Range(0, 40).Select(i => new FakeSkippedFile($"tasks/{i}.json", "Newer task format", true))]
        });
        using var screen = Opened(sync);
        var last = screen.Box("Deletion delay (days)");

        last.BringIntoView();

        _ = screen.WindowSize;
        var content = (FrameworkElement)screen.Window.Content;
        var top = last.TranslatePoint(new Point(0, 0), content).Y;
        Assert.InRange(top, 0, content.ActualHeight - last.ActualHeight);
    }

    private static ConfigureScreen Opened(FakeSyncService? sync = null)
    {
        var screen = new ConfigureScreen(sync: sync);
        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.Window.UpdateLayout();
        return screen;
    }

    private static Rect Bounds(FrameworkElement element, Window window) =>
        new(element.TranslatePoint(new Point(0, 0), window), new Size(element.ActualWidth, element.ActualHeight));

    private static bool VisibleButton(ConfigureScreen screen, string text) =>
        OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == text).IsVisible;
}
