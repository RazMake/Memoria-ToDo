using System.IO;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Resolving, reopening, and moving tasks from the list: the change goes to Tasks, and a known failure is reported with its reason.</summary>
[Unit]
public class ListActionsTests
{
    private readonly ListTasks _tasks = new();
    private readonly List<string> _reports = [];
    private readonly ListActions _actions;

    public ListActionsTests()
    {
        _actions = new ListActions(_tasks.Tasks, _reports.Add, Serilog.Core.Logger.None);
    }

    private string Id(string title) => _tasks.Tasks.All.Single(task => task.Title == title).Id;

    [Fact]
    public void Resolving_a_task_resolves_it()
    {
        var task = _tasks.Add("Buy milk");

        var done = _actions.Resolve(task.Id, resolved: true);

        Assert.True(done);
        Assert.NotNull(_tasks.Tasks.All.Single().Resolved);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Reopening_a_resolved_task_reopens_it()
    {
        var task = _tasks.AddResolved("Done one");

        var done = _actions.Resolve(task.Id, resolved: false);

        Assert.True(done);
        Assert.Null(_tasks.Tasks.All.Single().Resolved);
    }

    [Fact]
    public void A_resolve_that_cannot_be_saved_leaves_the_task_open_and_reports_why()
    {
        var task = _tasks.Add("Buy milk");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var done = _actions.Resolve(task.Id, resolved: true);

        Assert.False(done);
        Assert.Null(_tasks.Tasks.All.Single().Resolved);
        Assert.Contains("Writing is not allowed here", Assert.Single(_reports));
    }

    [Fact]
    public void Resolving_a_task_deleted_meanwhile_reports_that_it_no_longer_exists()
    {
        var task = _tasks.Add("Gone");
        _tasks.Tasks.Delete(task.Id);

        var done = _actions.Resolve(task.Id, resolved: true);

        Assert.False(done);
        Assert.Equal(["This task no longer exists."], _reports);
    }

    [Fact]
    public void Moving_puts_the_task_between_the_neighbors_and_keeps_the_new_rank()
    {
        _tasks.Add("C");
        _tasks.Add("B");
        _tasks.Add("A");

        var done = _actions.Move(new Move(Id("A"), Id("B"), Id("C")));

        Assert.True(done);
        Assert.Equal(["B", "A", "C"], _tasks.OpenTitlesInRankOrder());
    }

    [Fact]
    public void Moving_a_task_to_where_it_already_shows_changes_nothing()
    {
        _tasks.Add("C");
        _tasks.Add("B");
        var a = _tasks.Add("A");

        var done = _actions.Move(new Move(a.Id, null, Id("B")));

        Assert.True(done);
        Assert.Equal(a, _tasks.Tasks.All.Single(task => task.Title == "A"));
        Assert.Equal(["A", "B", "C"], _tasks.OpenTitlesInRankOrder());
    }

    [Fact]
    public void A_move_that_cannot_be_saved_leaves_the_order_and_reports_why()
    {
        _tasks.Add("B");
        var a = _tasks.Add("A");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var done = _actions.Move(new Move(a.Id, Id("B"), null));

        Assert.False(done);
        Assert.Equal(["A", "B"], _tasks.OpenTitlesInRankOrder());
        Assert.Contains("Writing is not allowed here", Assert.Single(_reports));
    }

    [Fact]
    public void Moving_a_task_deleted_meanwhile_reports_that_it_no_longer_exists()
    {
        var b = _tasks.Add("B");
        var gone = _tasks.Add("Gone");
        _tasks.Tasks.Delete(gone.Id);

        var done = _actions.Move(new Move(gone.Id, b.Id, null));

        Assert.False(done);
        Assert.Equal(["This task no longer exists."], _reports);
    }
}

/// <summary>The message the list shows for each failure a Tasks call is known to throw.</summary>
[Unit]
public class ListFailuresTests
{
    [Fact]
    public void Says_a_missing_task_no_longer_exists()
    {
        Assert.Equal("This task no longer exists.", ListFailures.MessageOf(new TaskNotFoundException()));
    }

    [Fact]
    public void Tells_the_user_to_choose_a_repository_folder_when_there_is_none()
    {
        Assert.Equal(
            "There is no repository folder yet. Open Configure and choose one.",
            ListFailures.MessageOf(new NoRepositoryFolderException()));
    }

    [Fact]
    public void Gives_a_file_or_data_failure_its_own_message()
    {
        Exception[] failures =
        [
            new IOException("The disk is full."),
            new UnauthorizedAccessException("Access denied."),
            new InvalidDataException("Bad task file."),
            new ArgumentException("An empty title."),
        ];

        Assert.Equal(
            ["The disk is full.", "Access denied.", "Bad task file.", "An empty title."],
            failures.Select(failure => ListFailures.MessageOf(failure)));
    }

    [Fact]
    public void Knows_no_message_for_an_incomplete_change_so_that_it_is_not_swallowed()
    {
        Assert.Null(ListFailures.MessageOf(new AggregateException()));
    }

    [Fact]
    public void Knows_no_message_for_any_other_failure_so_that_it_is_not_swallowed()
    {
        Assert.Null(ListFailures.MessageOf(new NotSupportedException("Unexpected.")));
    }
}
