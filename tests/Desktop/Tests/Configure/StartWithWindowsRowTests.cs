using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>The Start with Windows row of Configure's Startup section, built in memory with no window.</summary>
[Unit]
public class StartWithWindowsRowTests
{
    private readonly InMemoryLogSink _log = new();

    private StartWithWindowsRow Row(FakeStartWithWindows port) => new(port, _log.CreateLogger());

    private static CheckBox BoxOf(StartWithWindowsRow row) => row.Children.OfType<CheckBox>().Single();

    [WpfFact]
    public void The_box_is_checked_when_the_setting_is_on()
    {
        Assert.True(BoxOf(Row(new FakeStartWithWindows { On = true })).IsChecked);
    }

    [WpfFact]
    public void The_box_is_unchecked_when_the_setting_is_off()
    {
        Assert.False(BoxOf(Row(new FakeStartWithWindows { On = false })).IsChecked);
    }

    [WpfFact]
    public void Switching_the_box_on_sets_the_setting_at_once()
    {
        var port = new FakeStartWithWindows { On = false };
        var row = Row(port);

        BoxOf(row).IsChecked = true;

        Assert.Equal([true], port.SetCalls);
        Assert.True(BoxOf(row).IsChecked);
    }

    [WpfFact]
    public void A_failed_switch_shows_the_error_puts_the_box_back_and_logs_it()
    {
        var failure = new InvalidOperationException("Not installed.");
        var port = new FakeStartWithWindows { On = false, SetFailure = failure };
        var row = Row(port);

        BoxOf(row).IsChecked = true;

        Assert.Contains(
            row.Children.OfType<TextBlock>().Where(block => block.Visibility == System.Windows.Visibility.Visible).Select(OpenWindows.TextOf),
            text => text.Contains("Not installed.", StringComparison.Ordinal));
        Assert.False(BoxOf(row).IsChecked);
        Assert.Contains(_log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));
    }
}
