using Xunit;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The tray adapter, through <see cref="ITrayIcon"/>. Whether the icon is really in the notification area, and
/// what a click or a menu choice does, can't be seen from here without real mouse input; those are left out.
/// </summary>
[Integration]
public class TrayIconTests
{
    private static readonly IReadOnlyList<TrayMenuItem> Menu = [new TrayMenuItem("Add", () => { }), new TrayMenuItem("Exit", () => { })];

    [WpfFact]
    public void Shows_the_icon_with_its_tooltip_menu_and_left_click_without_failing()
    {
        var icon = new TrayIcon();
        try
        {
            icon.Show("Memoria", Menu, () => { });
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
