using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// A task row on its own, with no window: its right-click menu and the events the menu items raise, and how the row takes the keyboard.
/// The window's reaction to the events is in the Task View path tests.
/// </summary>
[Unit]
public class TaskRowTests
{
    private readonly TaskRow _row = new(new ListTasks().Add("Buy milk"), new FakeTheme(), new FakeDefaultBrowser(), new FakeDragPointer(), Serilog.Core.Logger.None);

    [WpfFact]
    public void The_right_click_menu_offers_Edit_Title_Edit_Description_and_Delete_with_their_keys()
    {
        var items = _row.ContextMenu!.Items.OfType<MenuItem>().Select(item => ((string)item.Header, item.InputGestureText));

        Assert.Equal([("Edit Title", "Shift+F2"), ("Edit Description", "F2"), ("Delete", "Del")], items);
    }

    [WpfFact]
    public void Each_menu_item_asks_for_its_own_action()
    {
        var raised = new List<string>();
        _row.EditTitleRequested += (_, _) => raised.Add("title");
        _row.EditRequested += (_, _) => raised.Add("description");
        _row.DeleteRequested += (_, _) => raised.Add("delete");

        foreach (var item in _row.ContextMenu!.Items.OfType<MenuItem>())
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }

        Assert.Equal(["title", "description", "delete"], raised);
    }

    [WpfFact]
    public void The_row_takes_the_keyboard_focus_and_draws_no_focus_rectangle()
    {
        Assert.True(_row.Focusable);
        Assert.Null(_row.FocusVisualStyle);
    }
}
