using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[Unit]
public class PopupListsTests
{
    private readonly ChoiceList _first = new();
    private readonly ChoiceList _second = new();
    private readonly PopupLists _popups;

    public PopupListsTests() => _popups = new PopupLists(_first, _second);

    [Fact]
    public void Showing_a_list_closes_the_one_that_was_open()
    {
        _popups.Show(_first, ["a"]);

        _popups.Show(_second, ["b"]);

        Assert.False(_first.IsOpen);
        Assert.Equal(["b"], _second.Items);
    }

    [Fact]
    public void Showing_a_list_again_refreshes_its_items()
    {
        _popups.Show(_first, ["a"]);

        _popups.Show(_first, ["x", "y"]);

        Assert.Equal(["x", "y"], _first.Items);
    }

    [Fact]
    public void Closing_closes_the_open_list_and_says_so()
    {
        _popups.Show(_second, ["b"]);

        Assert.True(_popups.CloseOpen());

        Assert.False(_second.IsOpen);
    }

    [Fact]
    public void Closing_with_none_open_says_nothing_was_open()
    {
        Assert.False(_popups.CloseOpen());
    }

    [Fact]
    public void Moving_down_moves_in_the_open_list()
    {
        _popups.Show(_second, ["a", "b"]);

        Assert.True(_popups.MoveDown());

        Assert.Equal(0, _second.SelectedIndex);
    }

    [Fact]
    public void Moving_down_with_none_open_says_so_so_Down_keeps_its_usual_meaning()
    {
        Assert.False(_popups.MoveDown());
    }

    [Fact]
    public void Enter_chooses_the_item_Down_moved_to()
    {
        var chosen = new List<int>();
        _first.Chosen += chosen.Add;
        _popups.Show(_first, ["a", "b"]);
        _popups.MoveDown();

        Assert.True(_popups.ChooseMovedOrClose());

        Assert.Equal([0], chosen);
    }

    [Fact]
    public void Enter_with_nothing_moved_to_closes_the_list_and_says_no_so_Enter_does_its_usual_thing()
    {
        _popups.Show(_first, ["a"]);

        Assert.False(_popups.ChooseMovedOrClose());

        Assert.False(_first.IsOpen);
    }

    [Fact]
    public void Enter_with_none_open_says_no()
    {
        Assert.False(_popups.ChooseMovedOrClose());
    }
}
