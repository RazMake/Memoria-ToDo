using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// The fourth global shortcut, the one that opens Contacts, by the shell design: Windows holds it only while contacts
/// are available, it keeps the shortcut Configure gave it while they aren't, and a taken shortcut opens Configure.
/// The switching table's other cells are in <c>TrayAppSwitchTests</c>.
/// </summary>
[Unit]
public class TrayAppContactsShortcutTests
{
    private static ShellHarness Started(bool available = true, bool startedAtSignIn = true)
    {
        var app = new ShellHarness(startedAtSignIn);
        app.Contacts.IsAvailable = available;
        app.Start();
        return app;
    }

    private static ShellHarness StartedWithPendingStartupChoice()
    {
        var app = new ShellHarness(startedAtSignIn: false);
        app.Sync.HoldStartupPublication = true;
        app.Contacts.IsAvailable = false;
        app.Start();
        return app;
    }

    [Fact]
    public void With_Add_open_closes_Add_then_shows_Contacts()
    {
        var app = Started();
        app.ActionOf(ShortcutKind.Contacts).Register(TestShortcut.A);
        app.Add.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Add", "Contacts"], app.Shown);
    }

    [Fact]
    public void With_Task_View_open_closes_it_and_shows_Contacts_when_it_closed()
    {
        var app = Started();
        app.ActionOf(ShortcutKind.Contacts).Register(TestShortcut.A);
        app.ViewTasks.IsOpen = true;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Task View", "Contacts"], app.Shown);
    }

    [Fact]
    public void With_Task_View_open_does_not_show_Contacts_when_Task_View_stays_open()
    {
        var app = Started();
        app.ActionOf(ShortcutKind.Contacts).Register(TestShortcut.A);
        app.ViewTasks.IsOpen = true;
        app.ViewTasks.CloseAnswer = false;

        app.GlobalShortcut.Press(TestShortcut.A);

        Assert.Equal(["Close Task View"], app.Shown);
    }

    [Fact]
    public async Task Registers_the_shortcut_with_Windows_while_contacts_are_available()
    {
        var app = Started();

        var registered = app.Configure.ContactsAction.Register(TestShortcut.ContactsDefault);

        Assert.True(registered);
        Assert.Equal([TestShortcut.ContactsDefault], app.GlobalShortcut.Registered);
        await app.Lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void While_contacts_are_unavailable_Register_stores_the_shortcut_checks_nothing_with_Windows_and_answers_true()
    {
        var app = Started(available: false);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);

        var registered = app.Configure.ContactsAction.Register(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal(0, app.GlobalShortcut.RegisterCount);
        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public void Contacts_becoming_available_registers_the_kept_shortcut_and_pressing_it_shows_Contacts()
    {
        var app = Started(available: false);
        app.Configure.ContactsAction.Register(TestShortcut.A);

        app.Contacts.IsAvailable = true;

        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
        app.GlobalShortcut.Press(TestShortcut.A);
        Assert.Equal(["Contacts"], app.Shown);
    }

    [Fact]
    public void Contacts_becoming_available_registers_nothing_when_no_shortcut_is_kept()
    {
        var app = Started(available: false);

        app.Contacts.IsAvailable = true;

        Assert.Equal(0, app.GlobalShortcut.RegisterCount);
        Assert.Empty(app.Shown);
    }

    [Fact]
    public void Contacts_becoming_available_registers_nothing_after_Unregister_forgot_the_shortcut()
    {
        var app = Started(available: false);
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.Configure.ContactsAction.Unregister();

        app.Contacts.IsAvailable = true;

        Assert.Equal(0, app.GlobalShortcut.RegisterCount);
    }

    [Fact]
    public void Contacts_becoming_available_registers_the_latest_shortcut_Configure_gave_while_they_were_unavailable()
    {
        var app = Started(available: false);
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.Configure.ContactsAction.Register(TestShortcut.B);

        app.Contacts.IsAvailable = true;

        Assert.Equal([TestShortcut.B], app.GlobalShortcut.Registered);
    }

    [Fact]
    public void Contacts_becoming_unavailable_releases_the_shortcut_and_it_registers_again_when_they_come_back()
    {
        var app = Started();
        app.Configure.ContactsAction.Register(TestShortcut.A);

        app.Contacts.IsAvailable = false;

        Assert.Empty(app.GlobalShortcut.Registered);

        app.Contacts.IsAvailable = true;

        Assert.Equal([TestShortcut.A], app.GlobalShortcut.Registered);
    }

    [Fact]
    public void Registering_the_shortcut_the_action_already_holds_answers_true_without_asking_Windows_again()
    {
        var app = Started();
        app.Configure.ContactsAction.Register(TestShortcut.A);

        var registered = app.Configure.ContactsAction.Register(TestShortcut.A);

        Assert.True(registered);
        Assert.Equal(1, app.GlobalShortcut.RegisterCount);
    }

    [Fact]
    public void Unregister_while_available_releases_the_shortcut_so_a_return_of_contacts_registers_nothing()
    {
        var app = Started();
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.Configure.ContactsAction.Unregister();
        var registerCount = app.GlobalShortcut.RegisterCount;

        app.Contacts.IsAvailable = false;
        app.Contacts.IsAvailable = true;

        Assert.Empty(app.GlobalShortcut.Registered);
        Assert.Equal(registerCount, app.GlobalShortcut.RegisterCount);
    }

    [Fact]
    public void A_shortcut_taken_when_contacts_become_available_opens_Configure_at_once_after_the_startup_choice_ran()
    {
        var app = Started(available: false, startedAtSignIn: true);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);
        app.Shown.Clear();

        app.Contacts.IsAvailable = true;

        Assert.Equal(["Configure"], app.Shown);
        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public void A_taken_shortcut_leaves_the_action_with_none_so_Configures_next_Register_goes_to_Windows_and_fails()
    {
        var app = Started(available: false, startedAtSignIn: true);
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);
        app.Contacts.IsAvailable = true;
        var registerCount = app.GlobalShortcut.RegisterCount;

        var registered = app.Configure.ContactsAction.Register(TestShortcut.A);

        Assert.False(registered);
        Assert.Equal(registerCount + 1, app.GlobalShortcut.RegisterCount);
    }

    [Fact]
    public void A_shortcut_taken_when_contacts_become_available_before_the_startup_choice_joins_it_so_Configure_opens_last()
    {
        var app = StartedWithPendingStartupChoice();
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);

        app.Contacts.IsAvailable = true;

        Assert.Empty(app.Shown);

        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["Task View", "Configure"], app.Shown);
    }

    [Fact]
    public void A_shortcut_taken_before_the_startup_choice_at_sign_in_opens_only_Configure()
    {
        var app = new ShellHarness(startedAtSignIn: true);
        app.Sync.HoldStartupPublication = true;
        app.Contacts.IsAvailable = false;
        app.Start();
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.GlobalShortcut.HoldByAnotherApp(TestShortcut.A);

        app.Contacts.IsAvailable = true;
        app.Sync.SetupControl.Publish(new FakeSyncSetupState { IsConfigured = true });

        Assert.Equal(["Configure"], app.Shown);
    }

    [Fact]
    public void A_port_failure_other_than_a_taken_shortcut_passes_on_when_contacts_become_available()
    {
        var app = Started(available: false);
        app.Configure.ContactsAction.Register(TestShortcut.A);
        app.GlobalShortcut.RegisterFailure = new InvalidOperationException("Windows failed.");

        var failure = Assert.Throws<InvalidOperationException>(() => app.Contacts.IsAvailable = true);

        Assert.Equal("Windows failed.", failure.Message);
        Assert.Empty(app.Shown);
    }

    [Fact]
    public async Task Stop_releases_the_Contacts_shortcut_too()
    {
        var app = Started();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ContactsAction.Register(TestShortcut.B);

        await app.Lifetime.StopAsync(CancellationToken.None);

        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public async Task Stop_stops_following_availability_so_a_late_notice_registers_nothing()
    {
        var app = Started(available: false);
        app.Configure.ContactsAction.Register(TestShortcut.A);
        await app.Lifetime.StopAsync(CancellationToken.None);

        app.Contacts.IsAvailable = true;

        Assert.Equal(0, app.GlobalShortcut.RegisterCount);
        Assert.Empty(app.GlobalShortcut.Registered);
    }

    [Fact]
    public async Task A_failed_release_of_the_Contacts_shortcut_still_releases_the_others_and_passes_on_the_failure()
    {
        var app = Started();
        app.Configure.AddNewTask.Register(TestShortcut.A);
        app.Configure.ContactsAction.Register(TestShortcut.ContactsDefault);
        app.GlobalShortcut.FailUnregister(TestShortcut.ContactsDefault, new InvalidOperationException("Windows won't release O."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Lifetime.StopAsync(CancellationToken.None));

        Assert.Equal("Windows won't release O.", error.Message);
        Assert.Equal([TestShortcut.ContactsDefault], app.GlobalShortcut.Registered);
        Assert.False(app.TrayIcon.IsShown);
    }
}
