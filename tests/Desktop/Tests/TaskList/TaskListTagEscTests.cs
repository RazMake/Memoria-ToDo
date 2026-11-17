using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Esc with the tag control's menu or Custom box open closes only that. The window and any edit stay.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListTagEscTests
{
    private static TaskListScreen ExpandedWithMenu(ListServices services)
    {
        services.Add("Buy milk", "", "home");
        var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        return screen;
    }

    [WpfFact]
    public void Esc_with_the_menu_open_closes_only_the_menu()
    {
        using var screen = ExpandedWithMenu(new ListServices());
        Assert.True(screen.TagMenuIsOpen);

        screen.Press(Key.Escape);

        Assert.False(screen.TagMenuIsOpen);
        Assert.True(screen.View.IsOpen);
        Assert.Equal(["Buy milk"], screen.SelectedTitles);
    }

    [WpfFact]
    public void Esc_with_Custom_open_closes_only_Custom_and_drops_its_text()
    {
        using var screen = ExpandedWithMenu(new ListServices());
        screen.ChooseCustom();
        screen.TypeTags("pend");
        Assert.True(screen.CustomIsOpen);

        screen.Press(Key.Escape);

        Assert.False(screen.CustomIsOpen);
        Assert.True(screen.View.IsOpen);
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        Assert.Equal("", screen.EditedTagsText);
    }

    [WpfFact]
    public void Esc_with_Custom_open_and_the_cursor_moved_to_Search_closes_only_Custom()
    {
        using var screen = ExpandedWithMenu(new ListServices());
        screen.ChooseCustom();
        screen.TypeTags("pend");
        screen.Press(Key.LeftCtrl, Key.F);
        Assert.True(screen.SearchHasFocus);

        screen.Press(Key.Escape);

        Assert.False(screen.CustomIsOpen);
        Assert.True(screen.View.IsOpen);
        Assert.Equal(["Buy milk"], screen.SelectedTitles);
    }

    [WpfFact]
    public void Esc_with_the_menu_open_and_the_cursor_moved_to_Search_closes_only_the_menu()
    {
        using var screen = ExpandedWithMenu(new ListServices());
        screen.Press(Key.LeftCtrl, Key.F);
        Assert.True(screen.SearchHasFocus);

        screen.Press(Key.Escape);

        Assert.False(screen.TagMenuIsOpen);
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_with_the_menu_open_during_an_edit_closes_the_menu_first_and_the_edit_stays()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.OpenTagMenu("Buy milk");
        Assert.True(screen.TagMenuIsOpen);

        screen.Press(Key.Escape);

        Assert.False(screen.TagMenuIsOpen);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void Esc_with_Custom_open_during_an_edit_closes_only_Custom_and_the_edit_stays()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");

        screen.Press(Key.Escape);

        Assert.False(screen.CustomIsOpen);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void During_an_edit_Esc_closes_the_menu_then_Custom_then_leaves_the_edit_one_step_per_press()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");
        screen.OpenTagMenu("Buy milk");
        Assert.True(screen.TagMenuIsOpen);
        Assert.True(screen.CustomIsOpen);

        screen.Press(Key.Escape);
        Assert.False(screen.TagMenuIsOpen);
        Assert.True(screen.CustomIsOpen);
        Assert.True(screen.IsEditing("Buy milk"));

        screen.Press(Key.Escape);
        Assert.False(screen.CustomIsOpen);
        Assert.True(screen.IsEditing("Buy milk"));

        screen.Press(Key.Escape);
        Assert.False(screen.IsEditingAny);
        Assert.True(screen.View.IsOpen);

        screen.PressToClose(Key.Escape);
        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void A_second_Esc_after_the_menu_closed_closes_Task_View()
    {
        using var screen = ExpandedWithMenu(new ListServices());
        screen.Press(Key.Escape);

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
    }
}
