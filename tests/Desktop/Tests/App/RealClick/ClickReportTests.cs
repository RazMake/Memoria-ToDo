using System.ComponentModel;
using Memoria.ToDo.Desktop.Platform.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class ClickReportTests
{
    [Fact]
    public void Reports_the_click_target_and_both_window_readings_even_when_no_input_was_observed()
    {
        var report = new ClickReport("Border 300x100; local (30, 20); screen (130.4, 170.4)")
        {
            AfterPress = "foreground 123 (target); capture none",
            AtTimeout = "foreground 456 (other); capture 789",
            PendingInput = "pending bits 0x0008",
        };

        var text = report.Describe();

        Assert.Contains("Border 300x100", text);
        Assert.Contains("local (30, 20); screen (130.4, 170.4)", text);
        Assert.Contains("foreground 123 (target); capture none", text);
        Assert.Contains("foreground 456 (other); capture 789", text);
        Assert.Contains("pending bits 0x0008", text);
        Assert.Contains("NativeDown: none", text);
        Assert.Contains("RoutedUp: none", text);
    }

    [Fact]
    public void Summarizes_each_input_stage_independently_with_its_count_times_and_latest_observation()
    {
        var report = new ClickReport("target");
        report.Record(ClickStage.NativeDown, TimeSpan.FromMilliseconds(3), "old point");
        report.Record(ClickStage.RoutedDown, TimeSpan.FromMilliseconds(5), "Border");
        report.Record(ClickStage.NativeDown, TimeSpan.FromMilliseconds(11), "new point");

        var text = report.Describe();

        Assert.Contains("NativeDown: count 2, first 3 ms, last 11 ms, latest new point", text);
        Assert.Contains("RoutedDown: count 1, first 5 ms, last 5 ms, latest Border", text);
        Assert.Contains("NativeUp: none", text);
        Assert.DoesNotContain("old point", text);
    }

    [Fact]
    public void Keeps_the_report_bounded_when_a_wait_observes_many_mouse_moves()
    {
        var report = new ClickReport("target");
        report.Record(ClickStage.NativeMove, TimeSpan.Zero, "point 0");
        var initialLength = report.Describe().Length;
        for (var index = 1; index < 100_000; index++)
        {
            report.Record(ClickStage.NativeMove, TimeSpan.FromMilliseconds(index), $"point {index}");
        }

        var text = report.Describe();

        Assert.Contains("count 100000, first 0 ms, last 99999 ms, latest point 99999", text);
        Assert.True(text.Length <= initialLength + 32, $"The report grew from {initialLength} to {text.Length} characters.");
        Assert.DoesNotContain("point 99998", text);
    }

    [Fact]
    public void Reports_refused_observations_as_unavailable_without_diagnosing_a_cause()
    {
        var report = new ClickReport("target")
        {
            AfterPress = PointerReading.Of("capture", () => throw new Win32Exception(5)),
        };
        report.NativeMessagesUnavailable("error 87");
        report.Record(ClickStage.RoutedUp, TimeSpan.FromMilliseconds(7), "Border");

        var text = report.Describe();

        Assert.Contains("capture unavailable (error 5)", text);
        Assert.Contains("NativeMove: unavailable (error 87)", text);
        Assert.Contains("NativeDown: unavailable (error 87)", text);
        Assert.Contains("NativeUp: unavailable (error 87)", text);
        Assert.Contains("RoutedUp: count 1", text);
        Assert.DoesNotContain("cause", text, StringComparison.OrdinalIgnoreCase);
    }
}
