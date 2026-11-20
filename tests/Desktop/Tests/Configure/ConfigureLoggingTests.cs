using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// What the Configure window logs through the logger it was given: every failure it catches and shows is also logged
/// at Error with the exception, so the log says what the window only summarizes.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureLoggingTests
{
    [WpfFact]
    public void A_failed_Auto_update_switch_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("The setting file is read-only.");
        using var screen = new ConfigureScreen();
        screen.Open();
        screen.Updates.SetAutoUpdateFailure = failure;

        screen.SwitchAutoUpdate();

        AssertErrorWith(screen, failure);
    }

    [WpfFact]
    public void A_failed_Start_with_Windows_switch_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("Not installed.");
        using var screen = new ConfigureScreen(new FakeStartWithWindows { On = false, SetFailure = failure });
        screen.Open();

        screen.SwitchStartWithWindows();

        AssertErrorWith(screen, failure);
    }

    [WpfFact]
    public void A_failed_Copy_public_key_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("Clipboard is busy.");
        using var screen = OpenedWithKey();
        screen.SetupActions.CopyFailure = failure;

        screen.Click("Copy public key");

        AssertErrorWith(screen, failure);
    }

    [WpfFact]
    public void A_failed_Generate_key_is_logged_as_an_error_with_the_exception()
    {
        var failure = new IOException("The key file is locked.");
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState { PublicKey = null, DeviceName = "Desk" });
        sync.SetupControl.Failure = failure;
        using var screen = new ConfigureScreen(sync: sync);
        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.Window.UpdateLayout();

        screen.Click("Generate key");

        AssertErrorWith(screen, failure);
    }

    [WpfFact]
    public void A_failed_Set_key_copy_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("Clipboard is busy.");
        var sync = WithKey();
        sync.SetupControl.AcceptedAddresses["git@github.com:me/tasks.git"] = new Uri("https://github.com/me/tasks/settings/keys/new");
        using var screen = Opened(sync);
        screen.SetupActions.CopyFailure = failure;
        screen.ChooseRepositoryAction("Clone a repo");
        screen.Box("Repository address").Text = "git@github.com:me/tasks.git";

        screen.Click("Set key");

        AssertErrorWith(screen, failure);
    }

    [WpfFact]
    public void Shared_settings_that_cannot_be_read_are_logged_as_an_error_with_the_exception()
    {
        var failure = new IOException("The settings file is locked.");
        using var screen = new ConfigureScreen(settings: new FakeSharedTaskSettings { ReadFailure = failure });

        screen.Open();

        AssertErrorWith(screen, failure);
    }

    private static void AssertErrorWith(ConfigureScreen screen, Exception failure) =>
        Assert.Contains(screen.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));

    [WpfFact]
    public void A_working_window_logs_no_problem()
    {
        using var screen = new ConfigureScreen();

        screen.Open();

        screen.Log.Events.AssertNoProblems();
    }

    private static FakeSyncService WithKey()
    {
        var sync = new FakeSyncService();
        sync.SetupControl.Publish(new FakeSyncSetupState { PublicKey = "ssh-rsa test-key Desk", DeviceName = "Desk" });
        return sync;
    }

    private static ConfigureScreen OpenedWithKey() => Opened(WithKey());

    private static ConfigureScreen Opened(FakeSyncService sync)
    {
        var screen = new ConfigureScreen(sync: sync);
        screen.Open();
        screen.ClickTitle("Sync settings");
        screen.Window.UpdateLayout();
        return screen;
    }
}
