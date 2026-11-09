using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Typed text matches a name from the start of any of its words, as the snippet picker and tag suggestions need.</summary>
[Unit]
public sealed class TypedTextMatchTests
{
    [Theory]
    [InlineData("Date stamp", "d")]
    [InlineData("Date stamp", "date")]
    [InlineData("Date stamp", "Date stamp")]
    public void Matches_from_the_start_of_the_name(string name, string typed)
    {
        Assert.True(TypedTextMatch.Matches(name, typed));
    }

    [Theory]
    [InlineData("Date stamp", "s")]
    [InlineData("Date stamp", "stamp")]
    [InlineData("Date stamp", "sta")]
    [InlineData("a b c", "b c")]
    [InlineData("a b c", "c")]
    public void Matches_from_the_start_of_any_later_word(string name, string typed)
    {
        Assert.True(TypedTextMatch.Matches(name, typed));
    }

    [Fact]
    public void Matches_typed_text_that_runs_across_words()
    {
        Assert.True(TypedTextMatch.Matches("Date stamp", "date s"));
    }

    [Theory]
    [InlineData("Date stamp", "DATE")]
    [InlineData("Date stamp", "STAMP")]
    [InlineData("DATE STAMP", "date s")]
    [InlineData("work", "WoRk")]
    public void Ignores_case(string name, string typed)
    {
        Assert.True(TypedTextMatch.Matches(name, typed));
    }

    [Theory]
    [InlineData("Date stamp", "ate")]
    [InlineData("Date stamp", "tamp")]
    [InlineData("Date stamp", "e s")]
    public void Does_not_match_from_the_middle_of_a_word(string name, string typed)
    {
        Assert.False(TypedTextMatch.Matches(name, typed));
    }

    [Theory]
    [InlineData("Date stamp", "stamp d")]
    [InlineData("Date stamp", "date stamps")]
    [InlineData("Date stamp", "x")]
    [InlineData("", "a")]
    public void Does_not_match_text_the_name_does_not_start_a_word_with(string name, string typed)
    {
        Assert.False(TypedTextMatch.Matches(name, typed));
    }

    [Theory]
    [InlineData("date-stamp", "stamp")]
    [InlineData("date_stamp", "stamp")]
    [InlineData("date\tstamp", "stamp")]
    public void Starts_a_word_only_after_a_space(string name, string typed)
    {
        Assert.False(TypedTextMatch.Matches(name, typed));
    }

    [Theory]
    [InlineData("Date stamp")]
    [InlineData("x")]
    [InlineData("")]
    public void Matches_every_name_when_the_typed_text_is_empty(string name)
    {
        Assert.True(TypedTextMatch.Matches(name, ""));
    }
}
