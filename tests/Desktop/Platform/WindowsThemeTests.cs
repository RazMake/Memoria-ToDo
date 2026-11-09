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
    public void Creating_it_without_a_WPF_application_throws_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => new WindowsTheme(UntouchableSetting()));
    }

    [Fact]
    public void Creating_it_without_a_WPF_application_neither_reads_nor_watches_the_setting()
    {
        var setting = UntouchableSetting();

        Assert.Throws<InvalidOperationException>(() => new WindowsTheme(setting));

        Assert.Equal(0, setting.Reads);
        Assert.Equal(0, setting.Listeners);
    }

    [Fact]
    public void Creating_it_on_a_thread_without_a_WPF_application_throws_InvalidOperationException()
    {
        var setting = UntouchableSetting();
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new WindowsTheme(setting);
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(0, setting.Reads);
        Assert.Equal(0, setting.Listeners);
    }

    // The shutdown tests trust FakeThemeSetting.Listeners to say whether the theme still watches the setting.
    [Fact]
    public void The_fake_setting_still_counts_a_watching_handler_after_a_handler_that_was_never_added_is_removed()
    {
        var setting = new FakeThemeSetting();
        var notified = 0;
        setting.MayHaveChanged += (_, _) => notified++;

        setting.MayHaveChanged -= (_, _) => { };

        Assert.Equal(1, setting.Listeners);
        setting.RaiseMayHaveChanged();
        Assert.Equal(1, notified);
    }

    [Fact]
    public void The_fake_setting_still_counts_a_watching_handler_after_removing_null()
    {
        var setting = new FakeThemeSetting();
        var notified = 0;
        setting.MayHaveChanged += (_, _) => notified++;

        setting.MayHaveChanged -= null;

        Assert.Equal(1, setting.Listeners);
        setting.RaiseMayHaveChanged();
        Assert.Equal(1, notified);
    }

    [Fact]
    public void The_fake_setting_counts_a_handler_added_twice_until_it_is_removed_twice()
    {
        var setting = new FakeThemeSetting();
        var notified = 0;
        EventHandler handler = (_, _) => notified++;
        setting.MayHaveChanged += handler;
        setting.MayHaveChanged += handler;

        setting.MayHaveChanged -= handler;

        Assert.Equal(1, setting.Listeners);
        setting.RaiseMayHaveChanged();
        Assert.Equal(1, notified);

        setting.MayHaveChanged -= handler;

        Assert.Equal(0, setting.Listeners);
        setting.RaiseMayHaveChanged();
        Assert.Equal(1, notified);
    }
}
