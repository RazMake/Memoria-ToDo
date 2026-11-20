using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;
using Serilog;


namespace Memoria.ToDo.Desktop.App;

/// <summary>Makes the Add and Task View windows from the one task store, so both paint a tag in the color the store keeps.</summary>
internal static class TaskWindows
{
    /// <param name="addLogger">The Task editor's logger.</param>
    /// <param name="viewLogger">The Task list's logger.</param>
    public static (IAddWindow Add, IViewTasksWindow ViewTasks) Create(ITaskStore tasks, IContacts contacts, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, Func<IDescriptionWebView> createWebView,
        IDefaultBrowser browser, ISyncService sync, CloseWindowKeys closeWindowKeys, IUpdateStatus updates, SaveKeys saveKeys, IDragPointer pointer,
        ILogger addLogger, ILogger viewLogger)
    {
        ArgumentNullException.ThrowIfNull(addLogger);
        ArgumentNullException.ThrowIfNull(viewLogger);
        return (AddWindow.Create(tasks, tasks.TagColors, contacts, Palette.Colors, theme, time, clipboard, imageFiles, files, closeWindowKeys, updates, saveKeys,
                     addLogger),
         ViewTasksWindow.Create(tasks, tasks.TagColors, tasks.TagOrder, contacts, Palette.Colors, theme, time, clipboard, imageFiles, files,
                     createWebView, browser, sync, closeWindowKeys, updates, saveKeys, pointer, viewLogger));
    }
}
