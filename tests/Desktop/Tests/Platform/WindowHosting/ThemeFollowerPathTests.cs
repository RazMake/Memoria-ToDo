using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>The theme follower on an element in a showing window, over the shared fake theme. One path from load to dispose.</summary>
[UX("WindowHosting")]
[Collection(ScreenTests.Collection)]
public class ThemeFollowerPathTests
{
    [WpfFact]
    public void Calls_back_on_load_and_each_switch_while_loaded_and_on_each_load_again_and_never_after_dispose()
    {
        using var screen = new FollowerScreen();

        screen.Show();
        Assert.Equal(1, screen.Calls);

        screen.Theme.Switch(true);
        screen.Theme.Switch(false);
        Assert.Equal(3, screen.Calls);

        screen.Remove();
        screen.Theme.Switch(true);
        Assert.Equal(3, screen.Calls);

        screen.Add();
        Assert.Equal(4, screen.Calls);

        screen.Follower.Dispose();
        screen.Theme.Switch(false);
        screen.Remove();
        screen.Add();
        screen.Theme.Switch(true);

        Assert.Equal(4, screen.Calls);
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
