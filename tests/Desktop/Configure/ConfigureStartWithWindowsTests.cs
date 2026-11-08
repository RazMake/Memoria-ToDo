using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>The Start with Windows check box. A failed switch shows the error's own message.</summary>
[UX]
public class ConfigureStartWithWindowsTests
{
    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_check_box_shows_whether_the_setting_is_on_when_the_window_opens(bool on)
    {
        using var screen = new ConfigureScreen(new FakeStartWithWindows { On = on });

        screen.Open();

        Assert.Equal(on, screen.StartWithWindowsBox.IsChecked);
    }

    [WpfFact]
    public void Switching_the_box_on_calls_Set_true_at_once()
    {
        var port = new FakeStartWithWindows { On = false };
        using var screen = new ConfigureScreen(port);
        screen.Open();

        screen.SwitchStartWithWindows();

        Assert.Equal([true], port.SetCalls);
        Assert.True(screen.StartWithWindowsBox.IsChecked);
    }

    [WpfFact]
    public void Switching_the_box_off_calls_Set_false_at_once()
    {
        var port = new FakeStartWithWindows { On = true };
        using var screen = new ConfigureScreen(port);
        screen.Open();

        screen.SwitchStartWithWindows();

        Assert.Equal([false], port.SetCalls);
        Assert.False(screen.StartWithWindowsBox.IsChecked);
    }

    [WpfFact]
    public void Opening_the_window_changes_nothing()
    {
        var port = new FakeStartWithWindows { On = true };
        using var screen = new ConfigureScreen(port);

        screen.Open();

        Assert.Empty(port.SetCalls);
    }

    [WpfFact]
    public void A_failed_switch_shows_the_error_and_keeps_the_previous_value()
    {
        var port = new FakeStartWithWindows { On = false, SetFailure = new InvalidOperationException("Not installed.") };
        using var screen = new ConfigureScreen(port);
        screen.Open();

        screen.SwitchStartWithWindows();

        Assert.True(screen.ShowsText("Not installed."));
        Assert.False(screen.StartWithWindowsBox.IsChecked);
    }
}
