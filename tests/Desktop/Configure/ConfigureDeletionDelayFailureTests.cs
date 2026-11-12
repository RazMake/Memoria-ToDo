using System.Text;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

[UX]
public class ConfigureDeletionDelayFailureTests
{
    private const string Clone = @"C:\Configure tests\clone";
    private const string SettingsFile = Clone + @"\settings.json";
    private const string BoxName = "Deletion delay (days)";
    private static readonly byte[] Unreadable = Encoding.UTF8.GetBytes("not json");

    [WpfFact]
    public void Opening_with_unreadable_settings_opens_the_window_and_reports_it_without_inventing_a_delay()
    {
        using var screen = new ConfigureScreen();
        Corrupt(screen);

        var failure = Record.Exception(screen.Open);
        screen.Expand("Sync settings");

        Assert.Null(failure);
        Assert.NotEqual("7", screen.Box(BoxName).Text);
        Assert.NotEmpty(screen.DeletionDelayMessage);
        Assert.Equal(Unreadable, screen.Files.ReadFile(SettingsFile));
    }

    [WpfFact]
    public void A_notice_that_settings_became_unreadable_keeps_the_shown_delay_and_reports_it()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Expand("Sync settings");

        var failure = Record.Exception(() => Corrupt(screen));

        Assert.Null(failure);
        Assert.Equal("7", screen.Box(BoxName).Text);
        Assert.NotEmpty(screen.DeletionDelayMessage);
        Assert.Equal(Unreadable, screen.Files.ReadFile(SettingsFile));
    }

    [WpfFact]
    public void A_later_readable_reload_shows_the_stored_delay_and_removes_the_read_failure()
    {
        using var screen = new ConfigureScreen(initialSettings: settings => settings.SetDeletionDelayDays(20));
        screen.Open();
        screen.Expand("Sync settings");
        var readable = screen.Files.ReadFile(SettingsFile);
        screen.Store.Settings.SetDeletionDelayDays(30);
        Corrupt(screen);
        Assert.NotEmpty(screen.DeletionDelayMessage);

        screen.Files.WriteFile(SettingsFile, readable);
        screen.Store.UseRepositoryFolder(Clone);

        Assert.Equal("20", screen.Box(BoxName).Text);
        Assert.Empty(screen.DeletionDelayMessage);
    }

    [WpfFact]
    public void Saving_a_delay_when_settings_are_unreadable_reports_it_and_keeps_the_shown_delay()
    {
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Expand("Sync settings");
        var field = screen.Box(BoxName);
        ConfigureScreen.Focus(field);
        Corrupt(screen);
        field.Text = "30";

        var failure = Record.Exception(screen.FocusStartWithWindowsBox);

        Assert.Null(failure);
        Assert.Equal("7", field.Text);
        Assert.NotEmpty(screen.DeletionDelayMessage);
        Assert.Equal(Unreadable, screen.Files.ReadFile(SettingsFile));
    }

    [WpfFact]
    public void A_save_refused_by_the_system_reports_it_and_keeps_the_shown_delay()
    {
        using var screen = new ConfigureScreen(
            wrapSettings: settings => new RefusingDelaySave(settings, new UnauthorizedAccessException("Access denied.")));
        screen.Open();
        screen.Expand("Sync settings");
        var field = screen.Box(BoxName);
        ConfigureScreen.Focus(field);
        field.Text = "30";

        var failure = Record.Exception(screen.FocusStartWithWindowsBox);

        Assert.Null(failure);
        Assert.Equal("7", field.Text);
        Assert.Equal(7, screen.Store.Settings.DeletionDelayDays);
        Assert.Contains("Access denied.", screen.DeletionDelayMessage);
    }

    // Makes the file unreadable and reloads it, as a sync that brought a bad settings.json does.
    private static void Corrupt(ConfigureScreen screen)
    {
        screen.Files.WriteFile(SettingsFile, Unreadable);
        screen.Store.UseRepositoryFolder(Clone);
    }

    /// <summary>The shared settings, except that saving the deletion delay fails with the given exception.</summary>
    private sealed class RefusingDelaySave(ISharedTaskSettings inner, Exception failure) : ISharedTaskSettings
    {
        public int DeletionDelayDays => inner.DeletionDelayDays;
        public string? AddNewTaskShortcut => inner.AddNewTaskShortcut;
        public string? ManageTasksShortcut => inner.ManageTasksShortcut;

        public event EventHandler? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        public void SetDeletionDelayDays(int days) => throw failure;
        public void SetAddNewTaskShortcut(string? shortcut) => inner.SetAddNewTaskShortcut(shortcut);
        public void SetManageTasksShortcut(string? shortcut) => inner.SetManageTasksShortcut(shortcut);
    }
}
