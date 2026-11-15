using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The theme follower on an element in a showing window, over the shared fake theme.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ThemeFollowerTests
{
    [WpfFact]
    public void Rejects_a_null_theme()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeFollower(new FrameworkElement(), null!, () => { }));
    }

    [WpfFact]
    public void Rejects_a_null_callback()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeFollower(new FrameworkElement(), new FakeTheme(), null!));
    }

    [WpfFact]
    public void Calls_back_once_when_the_element_loads()
    {
        using var screen = new FollowerScreen();

        screen.Show();

        Assert.Equal(1, screen.Calls);
    }

    [WpfFact]
    public void Calls_back_once_more_each_time_the_element_loads_again()
    {
        using var screen = new FollowerScreen();
        screen.Show();

        screen.Remove();
        screen.Add();

        Assert.Equal(2, screen.Calls);
    }

    [WpfFact]
    public void Calls_back_each_time_the_theme_switches_while_the_element_is_loaded()
    {
        using var screen = new FollowerScreen();
        screen.Show();

        screen.Theme.Switch(true);
        screen.Theme.Switch(false);

        Assert.Equal(3, screen.Calls);
    }

    [WpfFact]
    public void Does_not_call_back_for_a_switch_while_the_element_is_unloaded()
    {
        using var screen = new FollowerScreen();
        screen.Show();
        screen.Remove();
        var before = screen.Calls;

        screen.Theme.Switch(true);

        Assert.Equal(before, screen.Calls);
    }

    [WpfFact]
    public void Calls_back_when_the_element_loads_again_even_though_a_switch_was_missed_meanwhile()
    {
        using var screen = new FollowerScreen();
        screen.Show();
        screen.Remove();
        screen.Theme.Switch(true);
        var before = screen.Calls;

        screen.Add();

        Assert.Equal(before + 1, screen.Calls);
    }

    [WpfFact]
    public void Does_not_call_back_for_a_switch_after_it_is_disposed_while_loaded()
    {
        using var screen = new FollowerScreen();
        screen.Show();
        var before = screen.Calls;

        screen.Follower.Dispose();
        screen.Theme.Switch(true);

        Assert.Equal(before, screen.Calls);
    }

    [WpfFact]
    public void Does_not_call_back_when_it_is_disposed_before_the_element_loads()
    {
        using var screen = new FollowerScreen();
        screen.Follower.Dispose();

        screen.Show();
        screen.Theme.Switch(true);

        Assert.Equal(0, screen.Calls);
    }

    [WpfFact]
    public void Does_not_call_back_when_the_element_loads_again_after_it_is_disposed()
    {
        using var screen = new FollowerScreen();
        screen.Show();
        screen.Follower.Dispose();
        var before = screen.Calls;

        screen.Remove();
        screen.Add();
        screen.Theme.Switch(true);

        Assert.Equal(before, screen.Calls);
    }

    [WpfFact]
    public void Can_be_disposed_twice()
    {
        using var screen = new FollowerScreen();
        screen.Show();

        screen.Follower.Dispose();
        screen.Follower.Dispose();
    }

    /// <summary>
    /// A followed element inside a container in a window, which shows only when asked. Removing the element from its
    /// container unloads it, and adding it back loads it again. Each step returns once WPF has raised Loaded or Unloaded.
    /// </summary>
    private sealed class FollowerScreen : IDisposable
    {
        private readonly Border container = new();
        private readonly FrameworkElement element = new();
        private readonly Window window;

        public FollowerScreen()
        {
            Theme = new FakeTheme();
            Follower = new ThemeFollower(element, Theme, () => Calls++);
            container.Child = element;
            window = new Window { Content = container, Width = 200, Height = 100 };
        }

        public FakeTheme Theme { get; }

        public ThemeFollower Follower { get; }

        public int Calls { get; private set; }

        public void Show()
        {
            window.Show();
            Settle();
        }

        public void Remove()
        {
            container.Child = null;
            Settle();
        }

        public void Add()
        {
            container.Child = element;
            Settle();
        }

        public void Dispose() => window.Close();

        // Loaded and Unloaded are queued above Background priority.
        private void Settle() => window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }
}
