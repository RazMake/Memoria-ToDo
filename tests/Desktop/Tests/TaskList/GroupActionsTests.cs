using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Moving a group among the groups: the move goes to Tasks' tag order as the tags it lands between, and a known failure is reported with its reason.</summary>
[Unit]
public class GroupActionsTests
{
    private readonly FakeTagOrder _tagOrder = new();
    private readonly List<string> _reports = [];
    private readonly GroupActions _actions;

    public GroupActionsTests()
    {
        _tagOrder.Seed("home", "play", "work");
        _actions = new GroupActions(_tagOrder, _reports.Add, Serilog.Core.Logger.None);
    }

    private IReadOnlyList<string> Shown => _tagOrder.Sort(["home", "play", "work"]);

    [Fact]
    public void Moving_a_group_asks_the_tag_order_to_put_it_between_the_previous_and_the_next_tag()
    {
        var done = _actions.Move(new GroupMove("work", "home", "play"));

        Assert.True(done);
        Assert.Equal([("work", (string?)"home", (string?)"play")], _tagOrder.Moves);
        Assert.Equal(["home", "work", "play"], Shown);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Moving_a_group_to_an_end_leaves_that_side_open()
    {
        _actions.Move(new GroupMove("work", null, "home"));

        Assert.Equal([("work", (string?)null, (string?)"home")], _tagOrder.Moves);
        Assert.Equal(["work", "home", "play"], Shown);
    }

    [Fact]
    public void A_move_the_tag_order_refuses_is_reported_with_its_reason_and_the_groups_stay_where_they_were()
    {
        _tagOrder.Failure = new InvalidDataException("tags.json has a newer version.");

        var done = _actions.Move(new GroupMove("work", null, "home"));

        Assert.False(done);
        Assert.Equal("tags.json has a newer version.", Assert.Single(_reports));
        Assert.Equal(["home", "play", "work"], Shown);
    }

    [Fact]
    public void A_group_move_the_tag_order_saved_before_a_notice_failed_reports_that_and_counts_as_done()
    {
        _tagOrder.Failure = new NoticeFailedException<TaskItem>(null, [new IOException("tags.json notice failed.")]);

        var done = _actions.Move(new GroupMove("work", null, "home"));

        Assert.True(done);
        Assert.Equal(["Saved, but the app couldn't finish updating: tags.json notice failed."], _reports);
    }

    [Fact]
    public void A_group_move_that_may_be_only_partly_saved_reports_that_instead_of_throwing()
    {
        _tagOrder.Failure = new AggregateException(new IOException("tags.json is locked."), new IOException("Undo failed."));

        var done = _actions.Move(new GroupMove("work", null, "home"));

        Assert.False(done);
        Assert.Equal(["The change may be only partly saved: tags.json is locked."], _reports);
    }

    [Fact]
    public void A_failure_the_list_does_not_know_is_not_swallowed()
    {
        _tagOrder.Failure = new InvalidOperationException("Unexpected.");

        Assert.Throws<InvalidOperationException>(() => _actions.Move(new GroupMove("work", null, "home")));
        Assert.Empty(_reports);
    }
}
