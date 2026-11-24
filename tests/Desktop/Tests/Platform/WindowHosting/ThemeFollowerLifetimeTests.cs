using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>
/// Disposing a theme follower, on an element in no window: the test raises the element's Loaded event itself, as WPF does once
/// the element is in a showing window, which <see cref="ThemeFollowerPathTests"/> covers with a real window.
/// </summary>
[Unit]
public class ThemeFollowerLifetimeTests
{
    private static void Load(FrameworkElement element) => element.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

    [WpfFact]
    public void Does_not_call_back_when_it_is_disposed_before_the_element_loads()
    {
        var calls = 0;
        var theme = new FakeTheme();
        var element = new FrameworkElement();
        var follower = new ThemeFollower(element, theme, () => calls++);

        follower.Dispose();
        Load(element);
        theme.Switch(true);

        Assert.Equal(0, calls);
    }

    [WpfFact]
    public void Can_be_disposed_twice()
    {
        var follower = new ThemeFollower(new FrameworkElement(), new FakeTheme(), () => { });

        follower.Dispose();
        var second = Record.Exception(follower.Dispose);

        Assert.Null(second);
    }
}
