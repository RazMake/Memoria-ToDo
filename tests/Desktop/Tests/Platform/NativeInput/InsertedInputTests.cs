using System.ComponentModel;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class InsertedInputTests
{
    [Fact]
    public void Accepts_the_insertion_of_every_input()
    {
        InsertedInput.Require(inserted: 3, requested: 3, win32Error: 0);
    }

    [Theory]
    [InlineData(0u, 3, 5)]
    [InlineData(2u, 3, 87)]
    [InlineData(0u, 1, 0)]
    public void Fails_with_the_Windows_error_and_the_count_when_not_every_input_was_taken(uint inserted, int requested, int error)
    {
        var failure = Assert.Throws<Win32Exception>(() => InsertedInput.Require(inserted, requested, error));

        Assert.Equal(error, failure.NativeErrorCode);
        Assert.Contains($"{inserted} of {requested}", failure.Message);
    }
}
