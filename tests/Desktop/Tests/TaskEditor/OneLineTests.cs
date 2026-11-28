using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>A one-line field's paste rule: the pasted lines become one line.</summary>
[Unit]
public class OneLineTests
{
    [Theory]
    [InlineData("a\r\nb", "a b")]
    [InlineData("a\nb", "a b")]
    [InlineData("a\rb", "a b")]
    [InlineData("a\r\nb\nc\rd", "a b c d")]
    public void Lines_are_joined_with_one_space_whatever_their_break(string pasted, string expected)
    {
        Assert.Equal(expected, OneLine.Of(pasted));
    }

    [Fact]
    public void Each_line_is_trimmed_before_joining()
    {
        Assert.Equal("a b", OneLine.Of("  a  \n\t b "));
    }

    [Fact]
    public void A_trailing_line_break_leaves_no_trailing_space()
    {
        Assert.Equal("a", OneLine.Of("a\r\n"));
    }

    [Fact]
    public void A_single_line_keeps_the_spaces_inside_it()
    {
        Assert.Equal("a  b", OneLine.Of("a  b"));
    }

    [Fact]
    public void Nothing_stays_nothing()
    {
        Assert.Equal("", OneLine.Of(""));
    }
}
