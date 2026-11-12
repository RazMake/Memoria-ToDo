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
    public void Shows_the_icon_with_its_tooltip_menu_and_left_click_without_failing()
    {
        var icon = new TrayIcon();
        try
        {
            icon.Show(LongTooltip, Menu, () => { });
        }
        finally
        {
            icon.Remove();
        }
    }

    [WpfFact]
    public void Removes_a_shown_icon_without_failing()
    {
        var icon = new TrayIcon();
        icon.Show("Memoria", Menu, () => { });

        icon.Remove();
    }

    [WpfFact]
    public void Removing_an_icon_that_is_not_shown_does_nothing()
    {
        var icon = new TrayIcon();

        icon.Remove();
    }

    [WpfFact]
    public void Removing_an_icon_twice_does_nothing_the_second_time()
    {
        var icon = new TrayIcon();
        icon.Show("Memoria", Menu, () => { });
        icon.Remove();

        icon.Remove();
    }

    [WpfFact]
    public void Shows_the_icon_again_after_it_was_removed()
    {
        var icon = new TrayIcon();
        icon.Show("Memoria", Menu, () => { });
        icon.Remove();
        try
        {
            icon.Show("Memoria", Menu, () => { });
        }
        finally
        {
            icon.Remove();
        }
    }
}
