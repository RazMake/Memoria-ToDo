using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The install and uninstall steps the composition root gives to Velopack's hooks, <c>StartWithWindowsSteps</c>
/// with its <c>Install</c> and <c>Uninstall</c>. Velopack isn't started, so each test collects the failures a step reports.
/// </summary>
[Unit]
public class StartWithWindowsStepsTests
{
    private readonly FakeStartWithWindows setting = new();
    private readonly List<Exception> reported = [];

    private StartWithWindowsSteps Steps() => new(setting, reported.Add);

    [Fact]
    public void The_install_step_turns_the_setting_on_and_reports_nothing()
    {
        Steps().Install();

        Assert.True(setting.On);
        Assert.Empty(reported);
    }

    [Fact]
    public void The_uninstall_step_turns_the_setting_off_and_reports_nothing()
    {
        setting.On = true;

        Steps().Uninstall();

        Assert.False(setting.On);
        Assert.Empty(reported);
    }

    [Fact]
    public void A_failed_write_is_reported_and_does_not_stop_the_install()
    {
        var failure = new InvalidOperationException("not installed");
        setting.SetFailure = failure;

        Steps().Install();

        Assert.False(setting.On);
        Assert.Same(failure, Assert.Single(reported));
    }

    [Fact]
    public void A_failed_removal_is_reported_and_does_not_stop_the_uninstall()
    {
        setting.On = true;
        var failure = new InvalidOperationException("not installed");
        setting.SetFailure = failure;

        Steps().Uninstall();

        Assert.True(setting.On);
        Assert.Same(failure, Assert.Single(reported));
    }

    [Fact]
    public void A_failure_other_than_an_invalid_operation_is_not_swallowed()
    {
        setting.SetFailure = new UnauthorizedAccessException("denied");

        Assert.Throws<UnauthorizedAccessException>(() => Steps().Install());
        Assert.Throws<UnauthorizedAccessException>(() => Steps().Uninstall());
        Assert.Empty(reported);
    }

    [Fact]
    public void Creating_the_steps_without_a_setting_or_without_a_reporter_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StartWithWindowsSteps(null!, reported.Add));
        Assert.Throws<ArgumentNullException>(() => new StartWithWindowsSteps(setting, null!));
    }
}
