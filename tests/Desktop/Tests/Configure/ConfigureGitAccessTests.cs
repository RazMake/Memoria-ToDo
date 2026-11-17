using System.IO;
using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Git Access and Repository workflow on screen: Generate key, Copy public key and Forget key under Git Access,
/// Set key before Clone, and the host-trust buttons without their details string. What each action does and its
/// failures are unit-tested on DeviceActions and RepositoryChange; these check the wiring, the dialog, and the layout.
/// The Forget key dialog is modal, so <see cref="ClosePrompt"/> answers it while the click waits.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureGitAccessTests
{
    private const string KeyLine = "ssh-rsa test-key Desk";
    private const string GitHubAddress = "git@github.com:me/tasks.git";
    private static readonly Uri GitHubPage = new("https://github.com/me/tasks/settings/keys/new");
    private static readonly Uri ClonePage = new("https://clone.example/settings/keys");

    private const string ForgetText =
        "Delete this device's SSH key? Sync stops until you generate a new key and add it at your repository's host. "
        + "The old key stays listed at the host until you remove it there.";

    private readonly ClosePrompt _prompt = new("Forget key");

    // The key buttons.

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_SSH_key_row_shows_Generate_key_with_no_key_and_Copy_public_key_and_Forget_key_with_one(bool hasKey)
    {
        using var screen = Opened(WithKey(hasKey));

        Assert.Equal(!hasKey, screen.ShowsButton("Generate key"));
        Assert.Equal(hasKey, screen.ShowsButton("Copy public key"));
        Assert.Equal(hasKey, screen.ShowsButton("Forget key"));
    }

    [WpfFact]
    public void Generate_key_makes_the_key_copies_its_public_key_and_opens_no_page()
    {
        var sync = WithKey(false);
        sync.SetupControl.Publish(new FakeSyncSetupState { AddKeyPage = ClonePage });
        using var screen = Opened(sync);

        screen.Click("Generate key");

        Assert.Equal(1, sync.SetupControl.GenerateCount);
        Assert.Equal(sync.Setup.State.PublicKey, screen.SetupActions.CopiedPublicKey);
        Assert.Equal(["CopyPublicKey"], screen.SetupActions.Calls);
        Assert.Null(screen.SetupActions.OpenedKeyPage);
    }

    [WpfFact]
    public void After_Generate_key_the_row_shows_Copy_public_key_and_Forget_key_instead()
    {
        using var screen = Opened(WithKey(false));

        screen.Click("Generate key");

        Assert.False(screen.ShowsButton("Generate key"));
        Assert.True(screen.ShowsButton("Copy public key"));
        Assert.True(screen.ShowsButton("Forget key"));
    }

    [WpfFact]
    public void Copy_public_key_only_copies_the_key()
    {
        var sync = WithKey(true);
        sync.SetupControl.Publish(sync.SetupControl.Snapshot with { AddKeyPage = ClonePage });
        using var screen = Opened(sync);

        screen.Click("Copy public key");

        Assert.Equal(KeyLine, screen.SetupActions.CopiedPublicKey);
        Assert.Equal(["CopyPublicKey"], screen.SetupActions.Calls);
        Assert.Null(screen.SetupActions.OpenedKeyPage);
    }

    [WpfFact]
    public void A_key_button_message_shows_in_the_Git_Access_card()
    {
        using var screen = Opened(WithKey(true));
        screen.SetupActions.CopyFailure = new InvalidOperationException("Clipboard is busy.");

        screen.Click("Copy public key");

        Assert.True(screen.IsInCard(screen.TitleBlock("Clipboard is busy."), "Git Access"));
    }

    [WpfFact]
    public void Generate_key_has_a_tooltip_that_says_it_makes_the_key_and_copies_it()
    {
        using var screen = Opened(WithKey(false));

        var tip = ConfigureScreen.TipOf(screen.ButtonNamed("Generate key"));

        Assert.NotNull(tip);
        Assert.Contains("copies", tip, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("opens", tip, StringComparison.OrdinalIgnoreCase);
    }

    [WpfFact]
    public void Copy_public_key_and_Forget_key_have_tooltips_that_say_what_they_do()
    {
        using var screen = Opened(WithKey(true));

        var copy = ConfigureScreen.TipOf(screen.ButtonNamed("Copy public key"));
        var forget = ConfigureScreen.TipOf(screen.ButtonNamed("Forget key"));

        Assert.NotNull(copy);
        Assert.NotNull(forget);
        Assert.Contains("copies", copy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("opens", copy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delete", forget, StringComparison.OrdinalIgnoreCase);
    }

    // Forget key.

    [WpfFact]
    public void Forget_key_asks_first_in_a_modal_dialog_owned_by_and_centered_on_Configure()
    {
        using var screen = Opened(WithKey(true));

        ClickForget(screen, ClosePrompt.Click("Cancel"));

        Assert.Equal(1, _prompt.Shown);
        Assert.Same(screen.Window, _prompt.Owner);
        Assert.True(_prompt.Centered);
        Assert.Contains(ForgetText, _prompt.Texts);
    }

    [WpfFact]
    public void The_dialog_has_the_buttons_Forget_key_and_Cancel_and_only_Cancel_is_the_default()
    {
        using var screen = Opened(WithKey(true));
        string[] buttons = [];
        string[] defaults = [];

        ClickForget(screen, prompt =>
        {
            buttons = [.. OpenWindows.Descendants(prompt).OfType<Button>().Select(b => (string)b.Content)];
            defaults = [.. OpenWindows.Descendants(prompt).OfType<Button>().Where(b => b.IsDefault).Select(b => (string)b.Content)];
            OpenWindows.Click(prompt, "Cancel");
        });

        Assert.Equal(["Forget key", "Cancel"], buttons);
        Assert.Equal(["Cancel"], defaults);
    }

    [WpfTheory]
    [InlineData("Cancel")]
    [InlineData("Enter")]
    [InlineData("Esc")]
    [InlineData("Close")]
    public void Cancel_Enter_Esc_and_the_close_button_delete_nothing(string answer)
    {
        var sync = WithKey(true);
        using var screen = Opened(sync);
        Action<Window> pick = answer switch
        {
            "Cancel" => ClosePrompt.Click("Cancel"),
            "Enter" => ClosePrompt.PressEnter,
            "Esc" => ClosePrompt.PressEscape,
            _ => ClosePrompt.CloseIt,
        };

        ClickForget(screen, pick);

        Assert.Equal(0, sync.SetupControl.ForgetKeyCount);
        Assert.Equal(KeyLine, sync.Setup.State.PublicKey);
        Assert.True(screen.ShowsButton("Copy public key"));
        Assert.True(screen.ShowsButton("Forget key"));
        Assert.False(screen.ShowsButton("Generate key"));
    }

    [WpfFact]
    public void Enter_with_the_Forget_key_button_focused_still_picks_Cancel_and_deletes_nothing()
    {
        var sync = WithKey(true);
        using var screen = Opened(sync);

        ClickForget(screen, ClosePrompt.PressEnterOn("Forget key"));

        Assert.Equal(1, _prompt.Shown);
        Assert.Equal(0, sync.SetupControl.ForgetKeyCount);
        Assert.Equal(KeyLine, sync.Setup.State.PublicKey);
        Assert.True(screen.ShowsButton("Copy public key"));
        Assert.True(screen.ShowsButton("Forget key"));
        Assert.False(screen.ShowsButton("Generate key"));
    }

    [WpfFact]
    public void Confirming_deletes_the_device_key_and_the_row_shows_Generate_key_again()
    {
        var sync = WithKey(true);
        using var screen = Opened(sync);

        ClickForget(screen, ClosePrompt.Click("Forget key"));

        Assert.Equal(1, sync.SetupControl.ForgetKeyCount);
        Assert.Null(sync.Setup.State.PublicKey);
        Assert.True(screen.ShowsButton("Generate key"));
        Assert.False(screen.ShowsButton("Copy public key"));
        Assert.False(screen.ShowsButton("Forget key"));
    }

    [WpfFact]
    public void Confirming_never_accepts_a_host_key_and_keeps_the_Accept_host_key_button()
    {
        var sync = WithKey(true);
        sync.SetupControl.Publish(sync.SetupControl.Snapshot with { HostKey = HostKey(canAccept: true) });
        using var screen = Opened(sync);

        ClickForget(screen, ClosePrompt.Click("Forget key"));

        Assert.Null(sync.SetupControl.AcceptedFingerprint);
        Assert.True(screen.ShowsButton("Accept host key"));
    }

    [WpfFact]
    public void A_failed_deletion_shows_why_under_Git_Access_and_the_key_buttons_stay()
    {
        var sync = WithKey(true);
        using var screen = Opened(sync);
        sync.SetupControl.Failure = new IOException("The key file is locked.");

        ClickForget(screen, ClosePrompt.Click("Forget key"));

        Assert.True(screen.IsInCard(screen.TitleBlock("The key file is locked."), "Git Access"));
        Assert.True(screen.ShowsButton("Copy public key"));
        Assert.True(screen.ShowsButton("Forget key"));
        Assert.False(screen.ShowsButton("Generate key"));
    }

    // Accept host key shows only when the offered key can be accepted; no state shows a host or fingerprint.

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Accept_host_key_shows_in_Git_Access_only_when_acceptable_there_is_never_a_Forget_host_key_and_no_host_port_algorithm_or_fingerprint_string_shows(bool canAccept)
    {
        var sync = WithKey(true);
        sync.SetupControl.Publish(sync.SetupControl.Snapshot with { HostKey = HostKey(canAccept) });
        using var screen = Opened(sync);

        Assert.Equal(canAccept, screen.ShowsButton("Accept host key"));
        if (canAccept)
            Assert.True(screen.IsInCard(screen.ButtonNamed("Accept host key"), "Git Access"));
        Assert.False(screen.ShowsButton("Forget host key"));
        Assert.DoesNotContain(screen.Texts, text => text.Contains("self.example", StringComparison.Ordinal));
        Assert.DoesNotContain(screen.Texts, text => text.Contains("2222", StringComparison.Ordinal));
        Assert.DoesNotContain(screen.Texts, text => text.Contains("ssh-ed25519", StringComparison.Ordinal));
        Assert.DoesNotContain(screen.Texts, text => text.Contains("SHA256:", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Accept_host_key_accepts_the_offered_fingerprint()
    {
        var sync = WithKey(true);
        sync.SetupControl.Publish(sync.SetupControl.Snapshot with { HostKey = HostKey(canAccept: true) });
        using var screen = Opened(sync);

        screen.Click("Accept host key");

        Assert.Equal(sync.Setup.State.HostKey!.Fingerprint, sync.SetupControl.AcceptedFingerprint);
        Assert.Equal(0, sync.SetupControl.ForgetKeyCount);
    }

    [WpfFact]
    public void With_no_host_key_no_host_button_and_no_host_text_shows()
    {
        using var screen = Opened(WithKey(true));

        Assert.False(screen.ShowsButton("Accept host key"));
        Assert.False(screen.ShowsButton("Forget host key"));
        Assert.DoesNotContain(screen.Texts, text => text.StartsWith("Host ", StringComparison.Ordinal));
            }

    // Set key.

    [WpfFact]
    public void Set_key_sits_in_the_Repository_card_in_the_Clone_row_before_Clone_and_not_in_Git_Access()
    {
        using var screen = Opened(WithKey(true));

        screen.ChooseRepositoryAction("Clone a repo");

        var setKey = screen.ButtonNamed("Set key");
        var clone = screen.ButtonNamed("Clone");
        Assert.True(setKey.IsVisible);
        Assert.True(screen.IsInCard(setKey, "Repository"));
        Assert.False(screen.IsInCard(setKey, "Git Access"));
        var setBox = screen.BoundsOf(setKey);
        var cloneBox = screen.BoundsOf(clone);
        Assert.True(setBox.Right <= cloneBox.Left + 1, "Set key comes before Clone.");
        Assert.True(setBox.Top < cloneBox.Bottom && cloneBox.Top < setBox.Bottom, "They share one row.");
    }

    [WpfFact]
    public void Set_key_shows_only_for_the_Clone_choice()
    {
        using var screen = Opened(WithKey(true));

        Assert.False(screen.ShowsButton("Set key"));
        screen.ChooseRepositoryAction("Use existing repo clone");
        Assert.False(screen.ShowsButton("Set key"));
        screen.ChooseRepositoryAction("Clone a repo");
        Assert.True(screen.ShowsButton("Set key"));
    }

    [WpfFact]
    public void Set_key_is_enabled_only_while_the_address_box_holds_an_address_the_setup_accepts_even_with_no_key_or_clone()
    {
        var sync = WithKey(false);
        sync.SetupControl.AcceptedAddresses[GitHubAddress] = GitHubPage;
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Clone a repo");
        var setKey = screen.ButtonNamed("Set key");
        var address = screen.Box("Repository address");

        Assert.False(setKey.IsEnabled);
        address.Text = GitHubAddress;
        Assert.True(setKey.IsEnabled);
        address.Text = "not an address";
        Assert.False(setKey.IsEnabled);
        address.Text = "";
        Assert.False(setKey.IsEnabled);
        address.Text = GitHubAddress;
        Assert.True(setKey.IsEnabled);
    }

    [WpfFact]
    public void Set_key_with_no_key_asks_to_generate_one_first_under_Repository_without_copying_opening_or_generating()
    {
        var sync = WithKey(false);
        sync.SetupControl.AcceptedAddresses[GitHubAddress] = GitHubPage;
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = GitHubAddress;

        screen.Click("Set key");

        Assert.True(screen.IsInCard(screen.TitleBlock("Generate a key first."), "Repository"));
        Assert.Empty(screen.SetupActions.Calls);
        Assert.Equal(0, sync.SetupControl.GenerateCount);
        Assert.True(screen.ShowsButton("Generate key"));
    }

    [WpfFact]
    public void Set_key_copies_the_key_then_opens_the_page_of_the_typed_addresss_host_not_the_clones()
    {
        var sync = WithKey(true);
        sync.SetupControl.Publish(sync.SetupControl.Snapshot with
        {
            RepositoryAddress = "git@clone.example:me/tasks.git", AddKeyPage = ClonePage,
        });
        sync.SetupControl.AcceptedAddresses[GitHubAddress] = GitHubPage;
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = GitHubAddress;

        screen.Click("Set key");

        Assert.Equal(KeyLine, screen.SetupActions.CopiedPublicKey);
        Assert.Equal(GitHubPage, screen.SetupActions.OpenedKeyPage);
        Assert.Equal(["CopyPublicKey", "OpenKeyPage"], screen.SetupActions.Calls);
    }

    [WpfFact]
    public void Set_key_on_a_valid_host_with_no_known_page_copies_then_explains_under_Repository_and_opens_no_page()
    {
        var sync = WithKey(true);
        sync.SetupControl.AcceptedAddresses["git@git.example.org:me/tasks.git"] = null;
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = "git@git.example.org:me/tasks.git";

        screen.Click("Set key");

        Assert.Equal(["CopyPublicKey"], screen.SetupActions.Calls);
        Assert.True(screen.IsInCard(screen.TitleBlock("Copied. Add the key in your repository host's settings."), "Repository"));
    }

    [WpfFact]
    public void Set_key_never_saves_the_address_clones_or_syncs_and_leaves_the_address_typed()
    {
        var sync = WithKey(true);
        sync.SetupControl.AcceptedAddresses[GitHubAddress] = GitHubPage;
        using var screen = Opened(sync);
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = GitHubAddress;

        screen.Click("Set key");

        Assert.Empty(sync.SetupControl.Clones);
        Assert.Empty(sync.SetupControl.Selections);
        Assert.Equal(0, sync.SyncCount);
        Assert.Null(sync.Setup.State.RepositoryAddress);
        Assert.Equal(GitHubAddress, screen.Box("Repository address").Text);
        Assert.True(screen.ShowsText("Repository: None"));
    }

    [WpfFact]
    public void A_clipboard_failure_in_Set_key_shows_under_Repository_and_opens_no_page()
    {
        var sync = WithKey(true);
        sync.SetupControl.AcceptedAddresses[GitHubAddress] = GitHubPage;
        using var screen = Opened(sync);
        screen.SetupActions.CopyFailure = new InvalidOperationException("Clipboard is busy.");
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = GitHubAddress;

        screen.Click("Set key");

        Assert.True(screen.IsInCard(screen.TitleBlock("Clipboard is busy."), "Repository"));
        Assert.Null(screen.SetupActions.OpenedKeyPage);
    }

    [WpfFact]
    public void Set_key_explains_in_a_tooltip_that_it_copies_the_key_and_opens_the_hosts_page()
    {
        using var screen = Opened(WithKey(true));
        screen.ChooseRepositoryAction("Clone a repo");

        var tip = ConfigureScreen.TipOf(screen.ButtonNamed("Set key"));

        Assert.NotNull(tip);
        Assert.Contains("copies", tip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("opens", tip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("host", tip, StringComparison.OrdinalIgnoreCase);
    }

    private void ClickForget(ConfigureScreen screen, Action<Window> answer) =>
        _prompt.Run(() =>
        {
            screen.Click("Forget key");
            return 0;
        }, answer);

    private static FakeSshHostKey HostKey(bool canAccept) =>
        new("self.example", 2222, "ssh-ed25519", "SHA256:test-key", canAccept);

    private static FakeSyncService WithKey(bool hasKey)
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState { PublicKey = hasKey ? KeyLine : null, DeviceName = "Desk" });
        return sync;
    }

    private static ConfigureScreen Opened(FakeSyncService sync)
    {
        var screen = new ConfigureScreen(sync: sync);
        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.Window.UpdateLayout();
        return screen;
    }
}
