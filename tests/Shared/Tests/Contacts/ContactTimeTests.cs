using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A contact's time of day as text: written HH:mm, read as H:mm or HH:mm from 0:00 to 23:59.</summary>
[Unit]
public sealed class ContactTimeTests
{
    [Theory]
    [InlineData(9, 30, "09:30")]
    [InlineData(0, 0, "00:00")]
    [InlineData(9, 5, "09:05")]
    [InlineData(23, 59, "23:59")]
    [InlineData(12, 0, "12:00")]
    public void Writes_a_time_as_two_digit_hour_and_minute(int hour, int minute, string expected)
    {
        Assert.Equal(expected, ContactTime.TextOf(new TimeOnly(hour, minute)));
    }

    [Fact]
    public void Leaves_out_the_seconds_when_writing()
    {
        Assert.Equal("09:30", ContactTime.TextOf(new TimeOnly(9, 30, 45)));
    }

    [Theory]
    [InlineData("9:30", 9, 30)]
    [InlineData("09:30", 9, 30)]
    [InlineData("0:00", 0, 0)]
    [InlineData("00:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData("9:05", 9, 5)]
    [InlineData("12:00", 12, 0)]
    public void Reads_a_time_with_one_or_two_digit_hour(string text, int hour, int minute)
    {
        Assert.True(ContactTime.TryRead(text, out var time));

        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("24:01")]
    [InlineData("12:60")]
    [InlineData("9:5")]
    [InlineData("09:5")]
    [InlineData("9:30:15")]
    [InlineData("09:30:00")]
    [InlineData(" 9:30")]
    [InlineData("9:30 ")]
    [InlineData("9 :30")]
    [InlineData("9:3 0")]
    [InlineData("9.30")]
    [InlineData("930")]
    [InlineData("9:")]
    [InlineData(":30")]
    [InlineData("-1:30")]
    [InlineData("009:30")]
    [InlineData("9:30 AM")]
    [InlineData("noon")]
    [InlineData("ab:cd")]
    [InlineData("")]
    [InlineData(null)]
    public void Reads_nothing_from_text_that_is_not_a_time(string? text)
    {
        Assert.False(ContactTime.TryRead(text, out _));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 30)]
    [InlineData(23, 59)]
    public void Reads_back_the_text_it_writes(int hour, int minute)
    {
        var written = ContactTime.TextOf(new TimeOnly(hour, minute));

        Assert.True(ContactTime.TryRead(written, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }
}
