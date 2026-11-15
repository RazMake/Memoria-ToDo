using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Single window, with plain test windows. It's made on the UI thread, as its users make it.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class SingleWindowTests
{
    [WpfFact]
    public void The_first_Show_makes_one_window_and_shows_it()
    {
        using var made = new MadeWindows();
        var single = new SingleWindow(made.Make);
        Assert.Empty(made.All);

        single.Show();

        var window = Assert.Single(made.All);
        Assert.True(window.IsVisible);
    }

    [WpfFact]
    public void Show_while_the_window_is_open_makes_no_new_window_and_keeps_it_shown()
    {
        using var made = new MadeWindows();
        var single = new SingleWindow(made.Make);
        single.Show();

        single.Show();

        var window = Assert.Single(made.All);
        Assert.True(window.IsVisible);
    }

    [WpfFact]
    public void Show_after_the_user_closed_the_window_makes_a_new_window_and_shows_it()
    {
        using var made = new MadeWindows();
        var single = new SingleWindow(made.Make);
        single.Show();
        made.All[0].Close();

        single.Show();

        Assert.Equal(2, made.All.Count);
        Assert.True(made.All[1].IsVisible);
    }

    [WpfFact]
    public void Show_passes_on_the_functions_exception_and_calls_the_function_again_the_next_time()
    {
        using var made = new MadeWindows();
        var failure = new InvalidOperationException("no window");
        var calls = 0;
        var single = new SingleWindow(() => ++calls == 1 ? throw failure : made.Make());

        var thrown = Assert.Throws<InvalidOperationException>(single.Show);
        single.Show();

        Assert.Same(failure, thrown);
        Assert.Equal(2, calls);
        Assert.True(Assert.Single(made.All).IsVisible);
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
