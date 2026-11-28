using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Ctrl+Shift+C opens a list of the palette's colors; choosing one colors the selection of the field that opened it.</summary>
[Unit]
public class ColorPickingTests
{
    private readonly ChoiceList _colors = new();
    private readonly ChoiceList _other = new();
    private readonly PopupLists _popups;
    private readonly ColorPicking _picking;

    public ColorPickingTests()
    {
        _popups = new PopupLists(_colors, _other);
        _picking = new ColorPicking(["red", "orange", "blue"], _colors, _popups);
    }

    [Fact]
    public void Opening_lists_the_palette_colors_in_order_and_changes_no_text()
    {
        var field = new FakeTextField("urgent", 0, 6);

        _picking.Open(field);

        Assert.Equal(["red", "orange", "blue"], _colors.Items);
        Assert.Equal("urgent", field.Text);
    }

    [Fact]
    public void Opening_closes_another_open_list()
    {
        _popups.Show(_other, ["x"]);

        _picking.Open(new FakeTextField());

        Assert.False(_other.IsOpen);
    }

    [Fact]
    public void Choosing_a_color_wraps_the_selection_in_it_and_keeps_the_colored_text_selected()
    {
        var field = new FakeTextField("do urgent now", 3, 6);
        _picking.Open(field);

        _colors.Choose(2);

        Assert.Equal("do :blue[urgent] now", field.Text);
        Assert.Equal((9, 6), (field.SelectionStart, field.SelectionLength));
    }

    [Fact]
    public void Choosing_another_color_replaces_the_text_s_own_color()
    {
        var field = new FakeTextField(":red[urgent]", 5, 6);
        _picking.Open(field);

        _colors.Choose(2);

        Assert.Equal(":blue[urgent]", field.Text);
    }

    [Fact]
    public void Choosing_the_text_s_own_color_removes_it()
    {
        var field = new FakeTextField(":red[urgent]", 5, 6);
        _picking.Open(field);

        _colors.Choose(0);

        Assert.Equal("urgent", field.Text);
    }

    [Fact]
    public void Choosing_closes_the_list_and_puts_the_cursor_back_in_the_field()
    {
        var field = new FakeTextField("urgent", 0, 6);
        _picking.Open(field);

        _colors.Choose(0);

        Assert.False(_colors.IsOpen);
        Assert.Equal(1, field.FocusCount);
    }

    [Fact]
    public void The_color_goes_to_the_field_that_opened_the_list()
    {
        var title = new FakeTextField("a", 0, 1);
        var description = new FakeTextField("b", 0, 1);
        _picking.Open(title);
        _picking.Open(description);

        _colors.Choose(0);

        Assert.Equal("a", title.Text);
        Assert.Equal(":red[b]", description.Text);
    }

    [Fact]
    public void One_undo_reverts_a_chosen_color()
    {
        var field = new FakeTextField("urgent", 0, 6);
        _picking.Open(field);
        _colors.Choose(0);

        field.Undo();

        Assert.Equal("urgent", field.Text);
    }

    [Fact]
    public void Choosing_with_no_list_opened_for_a_field_fails_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => _colors.Choose(0));
    }

    [Fact]
    public void A_second_choice_after_the_list_closed_fails_loudly()
    {
        _picking.Open(new FakeTextField("a", 0, 1));
        _colors.Choose(0);

        Assert.Throws<InvalidOperationException>(() => _colors.Choose(0));
    }
}
