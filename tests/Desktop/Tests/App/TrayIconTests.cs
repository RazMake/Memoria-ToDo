using Xunit;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Testing;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The tray adapter, through <see cref="ITrayIcon"/>. Whether the icon is really in the notification area, and
/// what a click or a menu choice does, can't be seen from here without real mouse input; those are left out.
/// </summary>
[Integration]
public class TrayIconTests
{
    private static readonly IReadOnlyList<TrayMenuItem> Menu = [new TrayMenuItem("Add", () => { }), new TrayMenuItem("Exit", () => { })];

    // Far longer than the notification area shows (Windows keeps about 128 characters); only "does not fail" is checked.
    private static readonly string LongTooltip = "Memoria" + Environment.NewLine + new string('x', 1000);

    [WpfFact]
    public async Task Worker_thread_status_updates_can_be_drawn_and_queued_updates_after_removal_are_safe()
    {
        ITrayIcon icon = new TrayIcon();
        icon.Show("Memoria", Menu, () => { });
        try
        {
            await Task.Run(() => icon.UpdateStatus("Offline", TraySyncBadge.Offline));
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Run(() => icon.UpdateStatus(LongTooltip, TraySyncBadge.Warning));
            icon.Remove();
            await Task.Run(() => icon.UpdateStatus("Already closed", TraySyncBadge.None));
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        finally
        {
            icon.Remove();
        }
    }

    [WpfFact]
    public async Task The_badge_can_change_again_after_it_was_shown_and_cleared()
    {
        ITrayIcon icon = new TrayIcon();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var failures = new List<Exception>();

        // The update runs through BeginInvoke, so a failure in it would end the test host instead of this test.
        void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            failures.Add(e.Exception);
            e.Handled = true;
        }

        dispatcher.UnhandledException += OnUnhandled;
        icon.Show("Memoria", Menu, () => { });
        try
        {
            foreach (var badge in new[] { TraySyncBadge.Offline, TraySyncBadge.None, TraySyncBadge.Warning, TraySyncBadge.None })
            {
                icon.UpdateStatus("Memoria", badge);
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            }
        }
        finally
        {
            icon.Remove();
            dispatcher.UnhandledException -= OnUnhandled;
        }

        Assert.Empty(failures);
    }

    [WpfFact]
    public async Task The_menu_can_be_replaced_again_and_again_with_other_items_or_none()
    {
        ITrayIcon icon = new TrayIcon();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var failures = new List<Exception>();

        void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            failures.Add(e.Exception);
            e.Handled = true;
        }

        dispatcher.UnhandledException += OnUnhandled;
        icon.Show("Memoria", Menu, () => { });
        try
        {
            icon.UpdateMenu([new TrayMenuItem("Add", () => { }), new TrayMenuItem("Contacts", () => { }), new TrayMenuItem("Exit", () => { })]);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            icon.UpdateMenu([new TrayMenuItem("Exit", () => { })]);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            icon.UpdateMenu([]);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        finally
        {
            icon.Remove();
            dispatcher.UnhandledException -= OnUnhandled;
        }

        Assert.Empty(failures);
    }

    [WpfFact]
    public async Task Updating_the_menu_before_the_icon_is_shown_or_after_it_is_removed_is_ignored()
    {
        ITrayIcon icon = new TrayIcon();
        try
        {
            icon.UpdateMenu(Menu);
            icon.Show("Memoria", Menu, () => { });
            icon.Remove();
            icon.UpdateMenu(Menu);
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        finally
        {
            icon.Remove();
        }
    }

    [WpfFact]
    public void Updating_the_menu_refuses_a_missing_list()
    {
        ITrayIcon icon = new TrayIcon();
        icon.Show("Memoria", Menu, () => { });
        try
        {
            Assert.Throws<ArgumentNullException>(() => icon.UpdateMenu(null!));
        }
        finally
        {
            icon.Remove();
        }
    }

    [WpfFact]
    public void Shows_removes_and_shows_the_icon_again_and_removing_twice_or_before_showing_does_nothing()
    {
        var icon = new TrayIcon();
        icon.Remove();
        try
        {
            icon.Show(LongTooltip, Menu, () => { });
            icon.Remove();
            icon.Remove();
            icon.Show("Memoria", Menu, () => { });
        }
        finally
        {
            icon.Remove();
        }
    }
}
