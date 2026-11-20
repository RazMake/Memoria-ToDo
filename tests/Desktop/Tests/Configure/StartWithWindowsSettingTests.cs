using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>The Start with Windows decision behind the check box: what it shows, what it switches, and what it says when that fails.</summary>
[Unit]
public class StartWithWindowsSettingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Starts_from_the_current_setting_and_changes_nothing(bool on)
    {
        var port = new FakeStartWithWindows { On = on };

        var setting = new StartWithWindowsSetting(port, Serilog.Core.Logger.None);

        Assert.Equal(on, setting.IsOn);
        Assert.Null(setting.Problem);
        Assert.Empty(port.SetCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Switching_calls_the_port_once_and_shows_the_new_value(bool on)
    {
        var port = new FakeStartWithWindows { On = !on };
        var setting = new StartWithWindowsSetting(port, Serilog.Core.Logger.None);

        setting.Set(on);

        Assert.Equal([on], port.SetCalls);
        Assert.Equal(on, setting.IsOn);
        Assert.Null(setting.Problem);
    }

    [Fact]
    public void A_failed_switch_keeps_the_previous_value_and_shows_the_errors_message()
    {
        var port = new FakeStartWithWindows { On = false, SetFailure = new InvalidOperationException("Not installed.") };
        var setting = new StartWithWindowsSetting(port, Serilog.Core.Logger.None);

        setting.Set(true);

        Assert.False(setting.IsOn);
        Assert.Equal("Not installed.", setting.Problem);
    }

    [Fact]
    public void A_later_successful_switch_clears_the_problem()
    {
        var port = new FakeStartWithWindows { SetFailure = new InvalidOperationException("Not installed.") };
        var setting = new StartWithWindowsSetting(port, Serilog.Core.Logger.None);
        setting.Set(true);
        port.SetFailure = null;

        setting.Set(true);

        Assert.True(setting.IsOn);
        Assert.Null(setting.Problem);
    }

    [Fact]
    public void Rejects_a_missing_port()
    {
        Assert.Throws<ArgumentNullException>(() => new StartWithWindowsSetting(null!, Serilog.Core.Logger.None));
    }
}
