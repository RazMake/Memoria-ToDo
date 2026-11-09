using Microsoft.Win32;
using Xunit;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The Windows theme, each scenario a run of the platform's test program.</summary>
[Integration]
public class WindowsThemeIntegrationTests
{
    [Fact]
    public void IsDark_follows_the_apps_light_or_dark_setting_of_Windows()
    {
        using var host = HostProcess.Start("theme-read");

        host.WaitForExit();

        Assert.Contains($"theme is-dark={AppsAreDark()}", host.Lines);
    }

    [Fact]
    public void Creating_it_off_the_UI_thread_of_a_WPF_application_throws_InvalidOperationException()
    {
        using var host = HostProcess.Start("theme-off-ui-thread");

        host.WaitForExit();

        Assert.Contains("theme-create-error=InvalidOperationException", host.Lines);
        Assert.Contains("theme-source reads=0 listeners=0", host.Lines);
    }

    [Fact]
    public void A_switch_while_it_is_being_made_is_not_lost()
    {
        using var host = HostProcess.Start("theme-switch-during-creation");

        host.WaitForExit();

        Assert.Contains("theme-settled changes=1 is-dark=True", host.Lines);
    }

    [Fact]
    public void A_switch_from_a_background_thread_raises_Changed_once_on_the_UI_thread_with_IsDark_already_new()
    {
        using var host = HostProcess.Start("theme-switch-from-background-thread");

        host.WaitForExit();

        Assert.Equal(1, host.Count("theme-changed ui-thread=True is-dark=True"));
        Assert.Contains("theme-settled changes=1 is-dark=True", host.Lines);
    }

    [Fact]
    public void A_switch_to_dark_and_straight_back_that_the_UI_thread_never_sees_raises_nothing()
    {
        using var host = HostProcess.Start("theme-switch-and-back-unseen");

        host.WaitForExit();

        Assert.Contains("theme-settled changes=0 is-dark=False", host.Lines);
    }

    [Fact]
    public void An_error_in_a_Changed_handler_is_shown_and_the_app_ends_with_non_zero()
    {
        using var host = HostProcess.Start("theme-changed-handler-throws");

        var text = host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Contains("changed boom", text);
        Assert.NotEqual(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
    }

    [Fact]
    public void A_failed_read_while_handling_a_switch_is_shown_and_the_app_ends_with_non_zero()
    {
        using var host = HostProcess.Start("theme-read-fails-while-refreshing");

        var text = host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Contains("read boom", text);
        Assert.NotEqual(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
    }

    [Fact]
    public void It_watches_the_setting_while_the_app_runs_and_stops_after_the_UI_thread_shuts_down()
    {
        using var host = HostProcess.Start("theme-listeners-across-shutdown");

        host.WaitForExit();

        Assert.Contains("theme-listeners running=1", host.Lines);
        Assert.Contains("theme-listeners after-shutdown=0", host.Lines);
    }

    // The independent expectation: the setting itself, as Windows stores it, read only. WPF reads the app's value,
    // else the system's, else shows dark; a value that isn't a number counts as missing.
    private static bool AppsAreDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int appsLight ? appsLight == 0
            : key?.GetValue("SystemUsesLightTheme") is int systemLight ? systemLight == 0
            : true;
    }
}
