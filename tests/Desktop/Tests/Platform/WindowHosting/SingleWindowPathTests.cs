using System.Windows;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>Single window, with plain test windows. It's made on the UI thread, as its users make it. One path: the window is made on the first Show, reused while open, and made again after the user closed it.</summary>
[UX("WindowHosting")]
[Collection(ScreenTests.Collection)]
public class SingleWindowPathTests
{
    [WpfFact]
    public void Show_makes_the_window_once_while_it_is_open_and_makes_a_new_one_after_the_user_closed_it()
    {
        using var made = new MadeWindows();
        var single = new SingleWindow(made.Make, Logger.None);
        Assert.Empty(made.All);

        single.Show();

        var window = Assert.Single(made.All);
        Assert.True(window.IsVisible);

        single.Show();

        Assert.Same(window, Assert.Single(made.All));
        Assert.True(window.IsVisible);

        window.Close();
        single.Show();

        Assert.Equal(2, made.All.Count);
        Assert.True(made.All[1].IsVisible);
    }

    /// <summary>The windows a test's function made, in order, closed when the test ends.</summary>
    private sealed class MadeWindows : IDisposable
    {
        public List<Window> All { get; } = [];

        public Window Make()
        {
            var window = new Window { Width = 300, Height = 200 };
            All.Add(window);
            return window;
        }

        public void Dispose()
        {
            foreach (var window in All)
            {
                window.Close();
            }
        }
    }
}
