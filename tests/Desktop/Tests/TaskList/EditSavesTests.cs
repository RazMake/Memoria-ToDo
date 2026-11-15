using System.IO;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>What saving a Title edit or a Description edit asks of Tasks: only that field is new; everything else is as the task was.</summary>
[Unit]
public class EditSavesTests
{
    private readonly ListTasks _tasks = new();

    private TaskItem Stored(TaskItem task) => _tasks.Tasks.All.Single(t => t.Id == task.Id);

    [Fact]
    public void Saving_a_title_changes_only_the_title()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home", "errand");

        EditSaves.Title(_tasks.Tasks, started)("Buy oat milk");

        var stored = Stored(started);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal("Oat milk", stored.Description);
        Assert.Equal(["home", "errand"], stored.Tags);
        Assert.Equal(started.Rank, stored.Rank);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Saving_a_description_changes_only_the_description()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home", "errand");

        EditSaves.Description(_tasks.Tasks, started)("Oat milk, two cartons");

        var stored = Stored(started);
        Assert.Equal("Buy milk", stored.Title);
        Assert.Equal("Oat milk, two cartons", stored.Description);
        Assert.Equal(["home", "errand"], stored.Tags);
        Assert.Equal(started.Rank, stored.Rank);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void A_tag_changed_on_another_device_meanwhile_stays_when_a_title_is_saved()
    {
        var started = _tasks.Add("Buy milk", "", "home");
        _tasks.Tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, started.Description, ["home", "urgent"]));

        EditSaves.Title(_tasks.Tasks, started)("Buy oat milk");

        Assert.Equivalent(new[] { "home", "urgent" }, Stored(started).Tags);
        Assert.Equal("Buy oat milk", Stored(started).Title);
    }

    [Fact]
    public void A_title_changed_on_another_device_meanwhile_stays_when_a_description_is_saved()
    {
        var started = _tasks.Add("Buy milk", "Oat milk");
        _tasks.Tasks.SaveEdit(started, new TaskDraft(started.Id, "Buy oat milk", started.Description, started.Tags));

        EditSaves.Description(_tasks.Tasks, started)("Oat milk, two cartons");

        Assert.Equal("Buy oat milk", Stored(started).Title);
        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
    }

    [Fact]
    public void A_failed_save_propagates_for_the_editor_to_report_and_changes_nothing()
    {
        var started = _tasks.Add("Buy milk");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var failure = Assert.Throws<IOException>(() => EditSaves.Title(_tasks.Tasks, started)("Buy oat milk"));

        Assert.StartsWith("Writing is not allowed here", failure.Message);
        Assert.Equal("Buy milk", Stored(started).Title);
    }
}
