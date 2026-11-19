using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The list of open tasks for the task-reference snippet, narrowed by what the user types after it opens. The search and
/// the link text are Tasks' and Editing's, tested there.
/// </summary>
[Unit]
public class TaskReferencePickingTests
{
    private readonly EditorServices _services = new();
    private readonly ChoiceList _list = new();
    private readonly TaskReferencePicking _picking;
    private readonly FakeTextField _field = new("see ", 4);

    public TaskReferencePickingTests()
    {
        _services.UseRepository();
        _picking = new TaskReferencePicking(_services.Tasks, _services.Contacts, _list, new PopupLists(_list));
        _picking.Watch(_field);
    }

    private void Open(int replaceFrom = 4) => _picking.Open(_field, Snippets.TaskReference, replaceFrom);

    [Fact]
    public void Opening_lists_the_open_tasks_newest_first()
    {
        _services.AddTaskTitled("Buy milk");
        _services.AddTaskTitled("Call mom");

        Open();

        Assert.Equal(["Call mom", "Buy milk"], _list.Items);
    }

    [Fact]
    public void Resolved_tasks_are_not_listed()
    {
        _services.AddTaskTitled("Buy milk");
        _services.AddResolvedTask("Old chore");

        Open();

        Assert.Equal(["Buy milk"], _list.Items);
    }

    [Fact]
    public void A_title_is_listed_as_a_reader_sees_it_without_its_Markdown_marks()
    {
        _services.AddTaskTitled("**Buy** milk");

        Open();

        Assert.Equal(["Buy milk"], _list.Items);
    }

    [Fact]
    public void Opening_with_no_open_task_shows_no_list()
    {
        Open();

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Typing_narrows_the_list_by_the_search()
    {
        _services.AddTaskTitled("Buy milk");
        _services.AddTaskTitled("Call mom");
        Open();

        _field.Type("mil");

        Assert.Equal(["Buy milk"], _list.Items);
    }

    [Fact]
    public void The_list_closes_when_no_open_task_matches_what_is_typed()
    {
        _services.AddTaskTitled("Buy milk");
        Open();

        _field.Type("zzz");

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_the_cursor_moves_before_where_the_typing_started()
    {
        _services.AddTaskTitled("Buy milk");
        Open();
        _field.Type("m");

        _field.Cursor(2);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Selected_text_when_opening_is_not_used_to_narrow_the_list()
    {
        _services.AddTaskTitled("Buy milk");
        var field = new FakeTextField("abc", 0, 3);
        _picking.Watch(field);

        _picking.Open(field, Snippets.TaskReference, 0);

        Assert.Equal(["Buy milk"], _list.Items);
    }

    [Fact]
    public void Choosing_a_task_replaces_the_typed_text_with_a_link_to_it_and_closes_the_list()
    {
        var milk = _services.AddTaskTitled("Buy milk");
        _services.AddTaskTitled("Call mom");
        var field = new FakeTextField("a /task", 7);
        _picking.Watch(field);
        _picking.Open(field, Snippets.TaskReference, 2);
        field.Type("mil");

        _list.Choose(0);

        Assert.Equal($"a [Buy milk](task:{milk.Id})", field.Text);
        Assert.False(_list.IsOpen);
        Assert.Equal(1, field.FocusCount);
    }

    [Fact]
    public void The_link_goes_to_the_task_that_was_chosen()
    {
        var milk = _services.AddTaskTitled("Buy milk");
        var mom = _services.AddTaskTitled("Call mom");
        Open();

        _list.Choose(1);

        Assert.Equal($"see [Buy milk](task:{milk.Id})", _field.Text);
        Assert.DoesNotContain(mom.Id, _field.Text);
    }

    [Fact]
    public void One_undo_reverts_the_link()
    {
        _services.AddTaskTitled("Buy milk");
        Open();
        _list.Choose(0);

        _field.Undo();

        Assert.Equal("see ", _field.Text);
    }

    [Fact]
    public void Choosing_with_no_list_opened_fails_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => _list.Choose(0));
    }
}
