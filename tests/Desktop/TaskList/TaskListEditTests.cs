using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
public class TaskListEditTests
{
    [WpfFact]
    public void F2_turns_the_open_task_into_the_editor_in_its_place_with_its_text()
    {
        var services = new ListServices();
        services.Add("Second");
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.PressOn("Buy milk", Key.F2);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Buy milk", screen.EditorTitleText("Buy milk"));
        Assert.Equal(["Buy milk", "Second"], screen.RowTitles);
    }

    [WpfFact]
    public void The_Edit_button_does_the_same()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Click("Edit");

        Assert.True(screen.IsEditing("Buy milk"));
    }

    [WpfFact]
    public void Ctrl_Enter_saves_the_edit_and_leaves_it_with_the_task_open_and_formatted()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Buy oat milk"], screen.ExpandedTitles);
        Assert.Contains("Oat milk", screen.Web.Page);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_also_saves_and_leaves_the_edit()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);

        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Buy oat milk"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_save_that_fails_keeps_the_editor_open_with_the_text_and_says_why()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Buy oat milk", screen.EditorTitleText("Buy milk"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Esc_on_an_edit_with_no_change_leaves_it_with_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.EditorTitle("Buy milk").Focus();
        Keyboard.Focus(screen.EditorTitle("Buy milk"));

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Esc_on_a_changed_edit_discards_it_and_shows_the_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void Undo_reopens_the_discarded_edit_with_its_text()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.Press(Key.Escape);

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Buy oat milk", screen.EditorTitleText("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Opening_another_task_discards_a_changed_edit_with_an_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Call Ana", "About the invoice");
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.ClickRow("Call Ana");

        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Call Ana"], screen.ExpandedTitles);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal("Buy milk", services.Tasks.All.Single(task => task.Title.StartsWith("Buy")).Title);
    }

    [WpfFact]
    public void Tag_text_typed_and_not_yet_added_counts_as_a_change_so_Esc_gives_an_Undo_bar_that_restores_it()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTags("errand");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        screen.Click("Undo");
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("errand", screen.EditedTagsText);
    }

    [WpfFact]
    public void Saving_after_Undo_saves_the_edit_once()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.Press(Key.Escape);
        screen.Click("Undo");
        var changes = 0;
        services.Tasks.Changed += (_, _) => changes++;

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal(1, changes);
        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void Opening_another_task_after_an_edit_with_no_change_shows_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Call Ana");
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);

        screen.ClickRow("Call Ana");

        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
    }
}
