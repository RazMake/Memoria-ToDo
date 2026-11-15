using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class TextAndSelectionTests
{
    [Theory]
    [InlineData("hello", 1, 3)]
    [InlineData("hello", 2, 3)]
    [InlineData("hello", 0, 0)]
    [InlineData("hello", 5, 0)]
    [InlineData("", 0, 0)]
    public void Accepts_a_selection_inside_the_text_or_a_cursor_at_either_end(string text, int start, int length)
    {
        var field = new TextAndSelection(text, start, length);

        Assert.Equal((text, start, length), (field.Text, field.SelectionStart, field.SelectionLength));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, -1)]
    [InlineData(3, 3)]
    [InlineData(6, 0)]
    public void Rejects_a_negative_start_or_length_and_a_selection_that_ends_past_the_text(int start, int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextAndSelection("hello", start, length));
    }
}
