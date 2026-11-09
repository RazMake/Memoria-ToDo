using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class TextAndSelectionTests
{
    [Fact]
    public void Keeps_the_text_and_the_selection_it_was_given()
    {
        var field = new TextAndSelection("hello", 1, 3);

        Assert.Equal("hello", field.Text);
        Assert.Equal(1, field.SelectionStart);
        Assert.Equal(3, field.SelectionLength);
    }

    [Fact]
    public void Accepts_a_selection_that_ends_exactly_at_the_end_of_the_text()
    {
        var field = new TextAndSelection("hello", 2, 3);

        Assert.Equal(3, field.SelectionLength);
    }

    [Fact]
    public void Accepts_a_cursor_at_the_start_and_at_the_end_of_the_text()
    {
        Assert.Equal(0, new TextAndSelection("hello", 0, 0).SelectionStart);
        Assert.Equal(5, new TextAndSelection("hello", 5, 0).SelectionStart);
    }

    [Fact]
    public void Accepts_a_cursor_in_empty_text()
    {
        Assert.Equal(0, new TextAndSelection("", 0, 0).SelectionStart);
    }

    [Fact]
    public void Rejects_a_negative_start()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextAndSelection("hello", -1, 0));
    }

    [Fact]
    public void Rejects_a_negative_length()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextAndSelection("hello", 2, -1));
    }

    [Fact]
    public void Rejects_a_selection_that_ends_past_the_text()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextAndSelection("hello", 3, 3));
    }

    [Fact]
    public void Rejects_a_start_past_the_text()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextAndSelection("hello", 6, 0));
    }

    [Fact]
    public void Is_equal_to_another_field_with_the_same_text_and_selection()
    {
        Assert.Equal(new TextAndSelection("hello", 1, 3), new TextAndSelection("hello", 1, 3));
        Assert.NotEqual(new TextAndSelection("hello", 1, 3), new TextAndSelection("hello", 1, 2));
    }
}
