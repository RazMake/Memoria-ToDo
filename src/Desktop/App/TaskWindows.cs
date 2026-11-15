using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Makes the Add and View Tasks windows from the one task store, so both paint a tag in the color the store keeps.</summary>
internal static class TaskWindows
{
    public static (IAddWindow Add, IViewTasksWindow ViewTasks) Create(ITaskStore tasks, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, Func<IDescriptionWebView> createWebView,
        IDefaultBrowser browser, ISyncService sync, CloseWindowKeys closeWindowKeys) =>
        (AddWindow.Create(tasks, tasks.TagColors, Palette.Colors, theme, time, clipboard, imageFiles, files, closeWindowKeys),
         ViewTasksWindow.Create(tasks, tasks.TagColors, Palette.Colors, theme, time, clipboard, imageFiles, files,
             createWebView, browser, sync, closeWindowKeys));
}
