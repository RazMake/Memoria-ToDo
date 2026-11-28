using System.Windows;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The edit of a task's title in its row on the left. It leaves nothing behind: ending it unsaved drops what was typed, with no
/// Undo bar.
/// </summary>
internal sealed class TitleEdit : IFieldEdit
{
    private readonly ITasks _tasks;
    private readonly Action<string> _report;
    private readonly ILogger _logger;
    private readonly TitleEditor _editor;

    public TitleEdit(ITasks tasks, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time, IClipboard clipboard,
        SaveKeys saveKeys, TaskItem task, Action saveRequested, Action cancelRequested, Action<string> report, IPointerPosition pointer, ILogger logger)
    {
        _tasks = tasks;
        _report = report;
        _logger = logger;
        Started = task;
        Tags = task.Tags;
        _editor = new TitleEditor(tasks, contacts, palette, theme, time, clipboard, saveKeys, pointer, logger) { Value = task.Title };
        _editor.SaveRequested += (_, _) => saveRequested();
        _editor.CancelRequested += (_, _) => cancelRequested();
    }

    public FrameworkElement Editor => _editor;

    public TaskItem Started { get; }

    public bool ShowsInRow => true;

    public IReadOnlyList<string> Tags { get; set; }

    public bool HasChanges => _editor.HasChanges || EditSaves.TagsChanged(Started, Tags);

    public bool Save() => _editor.Save(EditSaves.Title(_tasks, Started, Tags, _report, _logger));

    public void FocusEnd() => _editor.FocusEnd();

    public void Leave(EditCleanup cleanup, Action restore)
    {
    }

    public void Close(EditCleanup cleanup)
    {
    }
}
