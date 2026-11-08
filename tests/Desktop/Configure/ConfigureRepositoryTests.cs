using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

[UX]
public class ConfigureRepositoryTests
{
    [WpfFact]
    public void Shows_none_for_the_repository_and_local_path_when_the_device_has_no_clone()
    {
        using var screen = Opened(new FakeRepositoryFolder());

        Assert.True(screen.ShowsText("Repository: None"));
        Assert.True(screen.ShowsText("Local Path: None"));
    }

    [WpfFact]
    public void Shows_the_current_repository_address_and_local_path()
    {
        var folder = new FakeRepositoryFolder { Folder = @"C:\Tasks" };
        folder.Remotes[@"C:\Tasks"] = "https://example.com/tasks.git";

        using var screen = Opened(folder);

        Assert.True(screen.ShowsText("Repository: https://example.com/tasks.git"));
        Assert.True(screen.ShowsText(@"Local Path: C:\Tasks"));
    }

    [WpfFact]
    public void Shows_why_the_repository_address_cannot_be_read()
    {
        using var screen = Opened(new FakeRepositoryFolder { Folder = @"C:\Tasks", RemoteFailure = new InvalidOperationException("git is missing.") });

        Assert.True(screen.ShowsText("git is missing."));
    }

    [WpfFact]
    public void Shows_the_identity_the_clone_commits_as()
    {
        var folder = new FakeRepositoryFolder { Folder = @"C:\Tasks" };
        folder.Identities[@"C:\Tasks"] = "Ana Pop <ana@example.com>";

        using var screen = Opened(folder);

        Assert.True(screen.ShowsText("Identity: Ana Pop <ana@example.com>"));
    }

    [WpfFact]
    public void Shows_none_for_the_identity_when_the_device_has_no_clone()
    {
        using var screen = Opened(new FakeRepositoryFolder());

        Assert.True(screen.ShowsText("Identity: None"));
    }

    [WpfTheory]
    [InlineData("Repository:")]
    [InlineData("Local Path:")]
    [InlineData("Identity:")]
    public void Shows_the_field_name_in_bold(string name)
    {
        using var screen = Opened(new FakeRepositoryFolder());

        var line = OpenWindows.Descendants(screen.Window).OfType<TextBlock>().Single(b => b.Text.StartsWith(name, StringComparison.Ordinal));
        var first = Assert.IsType<System.Windows.Documents.Run>(line.Inlines.FirstInline, exactMatch: false);
        Assert.Equal(name, first.Text);
        Assert.Equal(FontWeights.Bold, first.FontWeight);
    }

    [WpfFact]
    public void Clone_hides_the_boxes_as_soon_as_it_is_clicked()
    {
        var slow = new TaskCompletionSource();
        using var screen = Opened(new FakeRepositoryFolder { Pending = slow.Task });
        Choose(screen, "Clone a repo");
        VisibleBoxes(screen)[0].Text = "https://example.com/tasks.git";
        VisibleBoxes(screen)[1].Text = @"C:\New Tasks";

        Click(screen, "Clone");

        Assert.Empty(VisibleBoxes(screen));
        slow.SetResult();
        Settle(screen);
        Assert.True(screen.ShowsText(@"Local Path: C:\New Tasks"));
    }

    [WpfFact]
    public void Shows_no_boxes_until_a_configure_choice_is_made()
    {
        using var screen = Opened(new FakeRepositoryFolder());

        Assert.Empty(VisibleBoxes(screen));
    }

    [WpfFact]
    public void Clone_a_repo_shows_the_url_and_folder_boxes()
    {
        using var screen = Opened(new FakeRepositoryFolder());

        Choose(screen, "Clone a repo");

        Assert.Equal(2, VisibleBoxes(screen).Count);
        Assert.True(VisibleButton(screen, "Clone"));
        Assert.False(VisibleButton(screen, "Use this clone"));
    }

    [WpfFact]
    public void Use_existing_repo_clone_shows_only_the_folder_box_in_place_of_the_clone_boxes()
    {
        using var screen = Opened(new FakeRepositoryFolder());
        Choose(screen, "Clone a repo");

        Choose(screen, "Use existing repo clone");

        Assert.Single(VisibleBoxes(screen));
        Assert.True(VisibleButton(screen, "Use this clone"));
        Assert.False(VisibleButton(screen, "Clone"));
    }

    [WpfFact]
    public void Clone_clones_the_url_into_the_folder_shows_it_and_hides_the_boxes()
    {
        using var screen = Opened(new FakeRepositoryFolder());
        Choose(screen, "Clone a repo");

        VisibleBoxes(screen)[0].Text = "https://example.com/tasks.git";
        VisibleBoxes(screen)[1].Text = @"C:\New Tasks";
        Click(screen, "Clone");

        Assert.Equal([("https://example.com/tasks.git", @"C:\New Tasks")], screen.RepositoryFolder.Clones);
        Assert.True(screen.ShowsText("Repository: https://example.com/tasks.git"));
        Assert.True(screen.ShowsText(@"Local Path: C:\New Tasks"));
        Assert.Empty(VisibleBoxes(screen));
    }

    [WpfFact]
    public void Clone_without_a_url_or_folder_asks_for_them_and_clones_nothing()
    {
        using var screen = Opened(new FakeRepositoryFolder());
        Choose(screen, "Clone a repo");

        VisibleBoxes(screen)[0].Text = "https://example.com/tasks.git";
        Click(screen, "Clone");

        Assert.Empty(screen.RepositoryFolder.Clones);
        Assert.True(screen.ShowsText("Enter the repository's URL and the folder"));
    }

    [WpfFact]
    public void A_failed_clone_shows_the_error_and_keeps_the_current_folder()
    {
        using var screen = Opened(new FakeRepositoryFolder { Folder = @"C:\Old", Failure = new InvalidOperationException("Clone refused.") });
        Choose(screen, "Clone a repo");

        VisibleBoxes(screen)[0].Text = "https://example.com/tasks.git";
        VisibleBoxes(screen)[1].Text = @"C:\New";
        Click(screen, "Clone");

        Assert.True(screen.ShowsText("Clone refused."));
        Assert.True(screen.ShowsText(@"Local Path: C:\Old"));
        Assert.Equal(2, VisibleBoxes(screen).Count);
    }

    [WpfFact]
    public void Use_this_clone_makes_an_existing_folder_current_and_shows_its_address()
    {
        var folder = new FakeRepositoryFolder();
        folder.Remotes[@"C:\Existing"] = "https://example.com/existing.git";
        using var screen = Opened(folder);
        Choose(screen, "Use existing repo clone");

        VisibleBoxes(screen)[0].Text = @"C:\Existing";
        Click(screen, "Use this clone");

        Assert.Equal(@"C:\Existing", folder.Folder);
        Assert.True(screen.ShowsText("Repository: https://example.com/existing.git"));
        Assert.True(screen.ShowsText(@"Local Path: C:\Existing"));
        Assert.Empty(VisibleBoxes(screen));
    }

    [WpfFact]
    public void Use_this_clone_shows_why_a_folder_is_refused()
    {
        var folder = new FakeRepositoryFolder { Failure = new InvalidOperationException("Not a git clone.") };
        using var screen = Opened(folder);
        Choose(screen, "Use existing repo clone");

        VisibleBoxes(screen)[0].Text = @"C:\Plain";
        Click(screen, "Use this clone");

        Assert.True(screen.ShowsText("Not a git clone."));
        Assert.Null(folder.Folder);
    }

    private static ConfigureScreen Opened(FakeRepositoryFolder folder)
    {
        var screen = new ConfigureScreen(repositoryFolder: folder);
        screen.Open();
        screen.Expand("Sync settings");
        Settle(screen);
        return screen;
    }

    // The section's boxes the user can see; the two shortcut boxes come first in the window and are skipped.
    private static List<TextBox> VisibleBoxes(ConfigureScreen screen)
    {
        Settle(screen);
        return [.. OpenWindows.Descendants(screen.Window).OfType<TextBox>().Skip(2).Where(box => box.IsVisible)];
    }

    private static bool VisibleButton(ConfigureScreen screen, string text) =>
        OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == text).IsVisible;

    // Opens the Configure button's menu and picks the choice, as a user's clicks do.
    private static void Choose(ConfigureScreen screen, string choice)
    {
        var configure = OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == "Configure");
        Invoke(configure);
        var item = configure.ContextMenu.Items.OfType<MenuItem>().Single(i => (string)i.Header == choice);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        configure.ContextMenu.IsOpen = false;
        Settle(screen);
    }

    private static void Click(ConfigureScreen screen, string text)
    {
        Invoke(OpenWindows.Descendants(screen.Window).OfType<Button>().Single(b => b.Content as string == text));
        Settle(screen);
    }

    private static void Invoke(Button button) =>
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();

    // Runs pending work, such as the address read and the clone's continuation, and the layout that follows.
    private static void Settle(ConfigureScreen screen)
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        screen.Window.UpdateLayout();
    }
}
