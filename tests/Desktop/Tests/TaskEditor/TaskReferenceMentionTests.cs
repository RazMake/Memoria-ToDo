using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// How a task title's mentions appear in the task-reference list and in an inserted reference: as the contact's live text, or
/// the stored text when Contacts has none. Which text is a mention is Rendering's, tested there.
/// </summary>
[Unit]
public class TaskReferenceMentionTests
{
    private readonly EditorServices _services = new();
    private readonly ChoiceList _list = new();
    private readonly TaskReferencePicking _picking;
    private readonly FakeTextField _field = new("see ", 4);

    public TaskReferenceMentionTests()
    {
        _services.UseRepository();
        _services.UseContacts(["{fullName}"],         ContactFiles.Person("ana", "Ana Pop", ("firstName", "Ana")));
        _picking = new TaskReferencePicking(_services.Tasks, _services.Contacts, _list, new PopupLists(_list));
        _picking.Watch(_field);
    }

    private void Open() => _picking.Open(_field, Snippets.TaskReference, 4);

    [Fact]
    public void The_list_shows_a_mention_as_the_contacts_live_text()
    {
        _services.AddTaskTitled("Call [Old Name](contact:ana \"{fullName}\") today");

        Open();

        Assert.Equal(["Call Ana Pop today"], _list.Items);
    }

    [Fact]
    public void The_list_shows_a_mention_in_the_template_it_was_written_with()
    {
        _services.AddTaskTitled("Call [x](contact:ana \"{firstName}\")");

        Open();

        Assert.Equal(["Call Ana"], _list.Items);
    }

    [Fact]
    public void The_list_shows_the_stored_text_when_Contacts_has_no_such_contact()
    {
        _services.AddTaskTitled("Call [Zed Old](contact:zed \"{fullName}\")");

        Open();

        Assert.Equal(["Call Zed Old"], _list.Items);
    }

    [Fact]
    public void Typing_the_live_text_of_a_mention_that_is_not_in_the_stored_text_finds_no_task()
    {
        _services.AddTaskTitled("Call [Old Name](contact:ana \"{fullName}\")");
        _services.AddTaskTitled("Buy milk");
        Open();

        _field.Type("pop");

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Typing_the_stored_text_of_a_mention_finds_its_task_which_the_list_still_shows_with_the_live_text()
    {
        _services.AddTaskTitled("Call [Old Name](contact:ana \"{fullName}\")");
        _services.AddTaskTitled("Buy milk");
        Open();

        _field.Type("old");

        Assert.Equal(["Call Ana Pop"], _list.Items);
    }

    [Fact]
    public void The_list_shows_the_formatted_stored_text_of_an_unknown_contact_without_its_markers()
    {
        _services.AddTaskTitled("Call [**Zed**](contact:zed \"{fullName}\")");

        Open();

        Assert.Equal(["Call Zed"], _list.Items);
    }

    [Fact]
    public void Choosing_a_task_turns_each_mention_into_its_live_text_as_plain_text()
    {
        var task = _services.AddTaskTitled("Call [Old Name](contact:ana \"{fullName}\") and [Zed Old](contact:zed \"{fullName}\")");
        Open();

        _list.Choose(0);

        Assert.Equal($"see [Call Ana Pop and Zed Old](task:{task.Id})", _field.Text);
    }

    [Fact]
    public void The_live_text_of_a_mention_is_escaped_like_the_rest_of_the_reference_text()
    {
        _services.UseContacts(["{fullName}"], ContactFiles.Person("zed", "Zed [QA]"));
        var task = _services.AddTaskTitled("Call [Zed Old](contact:zed \"{fullName}\")");
        Open();

        _list.Choose(0);

        Assert.Equal($"see [Call Zed \\[QA\\]](task:{task.Id})", _field.Text);
    }
}
