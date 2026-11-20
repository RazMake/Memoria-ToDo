using System.IO;
using Memoria.ToDo.Shared.Fakes;
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
    public void A_failure_the_list_does_not_know_is_not_swallowed()
    {
        _tagOrder.Failure = new InvalidOperationException("Unexpected.");

        Assert.Throws<InvalidOperationException>(() => _actions.Move(new GroupMove("work", null, "home")));
        Assert.Empty(_reports);
    }
}
