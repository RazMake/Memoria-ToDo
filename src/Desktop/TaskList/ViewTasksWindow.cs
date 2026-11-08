using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>Creates the View Tasks window.</summary>
public static class ViewTasksWindow
{
    /// <summary>Creates View Tasks. The window opens only on <see cref="IViewTasksWindow.Show"/>.</summary>
    public static IViewTasksWindow Create() => new Opener(new SingleWindow(MakeWindow));

    private static Window MakeWindow() => new()
    {
        Title = "View Tasks",
        SizeToContent = SizeToContent.WidthAndHeight,
        MinWidth = 320,
        Content = new TextBlock
        {
            Text = "Viewing tasks is coming soon.",
            Margin = new Thickness(24),
        },
    };

    private sealed class Opener(SingleWindow window) : IViewTasksWindow
    {
        public void Show() => window.Show();

        public bool IsOpen => window.IsOpen;
    }
}

/// <summary>The View Tasks window, as Shell uses it.</summary>
public interface IViewTasksWindow
{
    /// <summary>Opens the window, or brings it forward.</summary>
    void Show();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }
}
