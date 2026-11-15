using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>Creates the View Tasks window.</summary>
public static class ViewTasksWindow
{
    /// <summary>
    /// Creates View Tasks. The window, and its description web view, are made only on <see cref="IViewTasksWindow.Show"/>;
    /// the web view is released when the window closes.
    /// </summary>
    public static IViewTasksWindow Create(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette, ITheme theme,
        TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
        Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, ISyncService sync, CloseWindowKeys closeWindowKeys)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(tagColors);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(imageFiles);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(createWebView);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(sync);
        return new Opener(tasks, tagColors, palette, theme, time, clipboard, imageFiles, files, createWebView, browser, sync);
    }

    // Holds a failure from a delete finished while the window closed, so the next window shows it, and the sync
    // request each opening starts. That request shows its own failure and never throws.
    private sealed class Opener : IViewTasksWindow
    {
        private readonly SingleWindow _window;
        private string? _carried;
        private TaskListWindow? _current;
        private Task _opening = Task.CompletedTask;

        public Opener(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
            IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
            Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, ISyncService sync)
        {
            _window = new SingleWindow(() =>
            {
                var message = _carried;
                var webView = createWebView();
                var listSync = new ListSync(sync);
                try
                {
                    var window = new TaskListWindow(tasks, tagColors, palette, theme, time, clipboard, imageFiles, files, webView,
                        browser, listSync, message, failure => _carried = failure);
                    _carried = null;
                    _current = window;
                    window.Closed += (_, _) => _current = null;
                    _opening = listSync.SyncAsync();
                    return window;
                }
                catch
                {
                    listSync.Dispose();
                    webView.Dispose();
                    throw;
                }
            });
        }

        public void Show() => _window.Show();

        public bool IsOpen => _window.IsOpen;

        public bool Close() => _current is not { } window || !_window.IsOpen || window.TryClose();
    }
}

/// <summary>The View Tasks window, as Shell uses it.</summary>
public interface IViewTasksWindow
{
    /// <summary>Opens the window, or brings it forward.</summary>
    void Show();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Closes the window as its close button does, first asking Save, Discard, or Cancel when an edit in place has
    /// changes. True when the window is closed, or was not open; false when the user cancels, Save doesn't save, or the
    /// prompt already shows.
    /// </summary>
    bool Close();
}
