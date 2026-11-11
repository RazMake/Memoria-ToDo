using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>Creates the View Tasks window.</summary>
public static class ViewTasksWindow
{
    /// <summary>
    /// Creates View Tasks. The window, and its description web view, are made only on <see cref="IViewTasksWindow.Show"/>;
    /// the web view is released when the window closes.
    /// </summary>
    public static IViewTasksWindow Create(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme,
        TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
        Func<IDescriptionWebView> createWebView, IDefaultBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(imageFiles);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(createWebView);
        ArgumentNullException.ThrowIfNull(browser);
        return new Opener(tasks, palette, theme, time, clipboard, imageFiles, files, createWebView, browser);
    }

    // Holds a failure from a delete finished while the window closed, so the next window shows it.
    private sealed class Opener : IViewTasksWindow
    {
        private readonly SingleWindow _window;
        private string? _carried;

        public Opener(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
            IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
            Func<IDescriptionWebView> createWebView, IDefaultBrowser browser)
        {
            _window = new SingleWindow(() =>
            {
                var message = _carried;
                _carried = null;
                return new TaskListWindow(tasks, palette, theme, time, clipboard, imageFiles, files, createWebView(), browser,
                    message, failure => _carried = failure);
            });
        }

        public void Show() => _window.Show();

        public bool IsOpen => _window.IsOpen;
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
