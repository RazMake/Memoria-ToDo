using Xunit;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class WindowsThemeTests
{
    // A setting that fails if the theme reads it; Reads and Listeners show whether it touched it at all.
    private static FakeThemeSetting UntouchableSetting() =>
        new() { ReadFailure = new InvalidOperationException("The setting must not be read.") };

    [Fact]
    public void Creating_it_without_a_setting_throws_ArgumentNullException_even_without_a_WPF_application()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsTheme(null!));
    }

    [Fact]
    public void Creating_it_without_a_WPF_application_throws_InvalidOperationException_and_neither_reads_nor_watches_the_setting()
    {
        var setting = UntouchableSetting();

        Assert.Throws<InvalidOperationException>(() => new WindowsTheme(setting));

        Assert.Equal(0, setting.Reads);
        Assert.Equal(0, setting.Listeners);
    }
}
