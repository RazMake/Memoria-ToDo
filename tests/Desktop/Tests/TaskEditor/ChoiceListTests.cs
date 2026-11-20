using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[Unit]
public class ChoiceListTests
{
    [Fact]
    public void Starts_closed_with_no_items_and_nothing_selected()
    {
        var list = new ChoiceList();

        Assert.False(list.IsOpen);
        Assert.Empty(list.Items);
        Assert.Equal(-1, list.SelectedIndex);
    }

    [Fact]
    public void Showing_items_opens_the_list_with_none_selected()
    {
        var list = new ChoiceList();

        list.Show(["a", "b"]);

        Assert.True(list.IsOpen);
        Assert.Equal(["a", "b"], list.Items);
        Assert.Equal(-1, list.SelectedIndex);
    }

    [Fact]
    public void Showing_again_replaces_the_items_and_clears_the_selection()
    {
        var list = new ChoiceList();
        list.Show(["a", "b"]);
        list.MoveDown();

        list.Show(["c"]);

        Assert.Equal(["c"], list.Items);
        Assert.Equal(-1, list.SelectedIndex);
    }

    [Fact]
    public void Closing_an_open_list_empties_it_and_says_it_was_open()
    {
        var list = new ChoiceList();
        list.Show(["a"]);

        Assert.True(list.Close());

        Assert.False(list.IsOpen);
        Assert.Empty(list.Items);
    }

    [Fact]
    public void Closing_a_closed_list_says_nothing_was_open()
    {
        Assert.False(new ChoiceList().Close());
    }

    [Fact]
    public void Moving_down_selects_the_first_item_then_each_next_one()
    {
        var list = new ChoiceList();
        list.Show(["a", "b", "c"]);

        list.MoveDown();
        Assert.Equal(0, list.SelectedIndex);

        list.MoveDown();
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void Moving_down_stays_on_the_last_item()
    {
        var list = new ChoiceList();
        list.Show(["a", "b"]);

        list.MoveDown();
        list.MoveDown();
        list.MoveDown();

        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void Choosing_the_moved_to_item_raises_Chosen_with_its_position_and_keeps_the_list_open()
    {
        var list = new ChoiceList();
        var chosen = new List<int>();
        list.Chosen += chosen.Add;
        list.Show(["a", "b"]);
        list.MoveDown();
        list.MoveDown();

        Assert.True(list.ChooseMoved());

        Assert.Equal([1], chosen);
        Assert.True(list.IsOpen);
    }

    [Fact]
    public void Moving_up_selects_the_item_before_and_stays_on_the_first()
    {
        var list = new ChoiceList();
        list.Show(["a", "b", "c"]);
        list.MoveDown();
        list.MoveDown();
        list.MoveDown();

        list.MoveUp();
        Assert.Equal(1, list.SelectedIndex);

        list.MoveUp();
        list.MoveUp();
        Assert.Equal(0, list.SelectedIndex);
    }

    [Fact]
    public void Moving_up_with_nothing_moved_to_leaves_nothing_moved_to()
    {
        var list = new ChoiceList();
        list.Show(["a", "b"]);

        list.MoveUp();

        Assert.Equal(-1, list.SelectedIndex);
    }

    [Fact]
    public void Choosing_the_moved_to_item_when_none_was_moved_to_does_nothing_and_says_so()
    {
        var list = new ChoiceList();
        var chosen = new List<int>();
        list.Chosen += chosen.Add;
        list.Show(["a"]);

        Assert.False(list.ChooseMoved());

        Assert.Empty(chosen);
    }

    [Fact]
    public void Choosing_a_position_as_a_click_does_raises_Chosen_with_it()
    {
        var list = new ChoiceList();
        var chosen = new List<int>();
        list.Chosen += chosen.Add;
        list.Show(["a", "b", "c"]);

        list.Choose(2);

        Assert.Equal([2], chosen);
    }

    [Fact]
    public void Changed_is_raised_by_showing_moving_and_closing_but_not_by_closing_a_closed_list()
    {
        var list = new ChoiceList();
        var changes = 0;
        list.Changed += () => changes++;

        list.Show(["a"]);
        list.MoveDown();
        list.Close();
        list.Close();

        Assert.Equal(3, changes);
    }
}
