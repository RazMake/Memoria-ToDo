using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Shared.Sync;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>Creates the Task View window.</summary>
public static class ViewTasksWindow
{
    /// <summary>
    /// Creates Task View. The window, and its description web view, are made only on <see cref="IViewTasksWindow.Show"/>;
    /// the web view is released when the window closes.
    /// </summary>
    public static IViewTasksWindow Create(ITasks tasks, ITagColors tagColors, ITagOrder tagOrder, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme,
        TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
        Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, ISyncService sync, CloseWindowKeys closeWindowKeys,
        IUpdateStatus updates, SaveKeys saveKeys, IDragPointer pointer, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(tagColors);
        ArgumentNullException.ThrowIfNull(tagOrder);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(imageFiles);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(createWebView);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(closeWindowKeys);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(saveKeys);
        ArgumentNullException.ThrowIfNull(pointer);
        ArgumentNullException.ThrowIfNull(logger);
        return new Opener(tasks, tagColors, tagOrder, contacts, palette, theme, time, clipboard, imageFiles, files, createWebView, browser, sync, closeWindowKeys, updates, saveKeys, pointer, logger);
    }

    // Holds a failure from a delete finished while the window closed, so the next window shows it, and the sync
    // request each opening starts. That request shows its own failure and never throws.
    private sealed class Opener : IViewTasksWindow
    {
        private readonly SingleWindow _window;
        private string? _carried;
        private TaskListWindow? _current;
        private Task _opening = Task.CompletedTask;

        public Opener(ITasks tasks, ITagColors tagColors, ITagOrder tagOrder, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
            IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
            Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, ISyncService sync, CloseWindowKeys closeWindowKeys,
            IUpdateStatus updates, SaveKeys saveKeys, IDragPointer pointer, ILogger logger)
        {
            _window = new SingleWindow(() =>
            {
                var message = _carried;
                var webView = createWebView();
                var listSync = new ListSync(sync, palette, theme, logger);
                try
                {
                    var window = new TaskListWindow(tasks, tagColors, tagOrder, contacts, palette, theme, time, clipboard, imageFiles, files, webView,
                        browser, listSync, updates, saveKeys, pointer, message, failure => _carried = failure, logger);
                    _carried = null;
                    closeWindowKeys.Attach(window);
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

        public void Show()
        {
            var wasOpen = _window.IsOpen;
            _window.Show();
            if (wasOpen)
            {
                _current?.StartSearch();
            }
        }

        public bool IsOpen => _window.IsOpen;

        public bool Close() => _current is not { } window || !_window.IsOpen || window.TryClose();
    }
}

/// <summary>The Task View window, as Shell uses it.</summary>
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
