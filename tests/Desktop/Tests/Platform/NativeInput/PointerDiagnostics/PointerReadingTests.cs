using System.ComponentModel;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class PointerReadingTests
{
    [Fact]
    public void Shows_the_value_that_was_read_under_its_name()
    {
        var text = PointerReading.Of("foreground", () => "1A2B");

        Assert.Contains("foreground", text);
        Assert.Contains("1A2B", text);
    }

    [Fact]
    public void Shows_a_reading_windows_refuses_as_unavailable_with_its_error_instead_of_throwing()
    {
        var text = PointerReading.Of("capture", () => throw new Win32Exception(1400));

        Assert.Contains("capture", text);
        Assert.Contains("unavailable", text);
        Assert.Contains("1400", text);
    }

    [Fact]
    public void Does_not_hide_a_failure_that_is_not_a_windows_refusal()
    {
        Assert.Throws<InvalidOperationException>(() => PointerReading.Of("cursor", () => throw new InvalidOperationException("bug")));
    }
}