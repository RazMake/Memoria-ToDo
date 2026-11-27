using Memoria.ToDo.Android.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.Platform.Tests;

[Unit]
public sealed class ScreenServices_holding_what_a_screen_needs
{
    private sealed class Needs;

    [Fact]
    public void gives_back_what_was_set()
    {
        var services = new ScreenServices<Needs>();
        var needs = new Needs();

        services.Set(needs);

        Assert.Same(needs, services.Get());
    }

    [Fact]
    public void gives_back_the_latest_when_set_again()
    {
        var services = new ScreenServices<Needs>();
        services.Set(new Needs());
        var latest = new Needs();

        services.Set(latest);

        Assert.Same(latest, services.Get());
    }

    [Fact]
    public void rejects_setting_null()
    {
        var services = new ScreenServices<Needs>();

        Assert.Throws<ArgumentNullException>(() => services.Set(null!));
    }

    [Fact]
    public void fails_naming_the_type_when_read_before_anything_was_set()
    {
        var services = new ScreenServices<Needs>();

        var failure = Assert.Throws<InvalidOperationException>(() => services.Get());

        Assert.Contains(nameof(Needs), failure.Message);
    }
}
