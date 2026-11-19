using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The edit of a task's description on the right side, images included. Leaving it unsaved keeps a changed one on the Undo bar,
/// and its images with it.
/// </summary>
internal sealed class DescriptionEdit : IDescriptionEdit
{
    private readonly ITasks _tasks;
    private readonly DescriptionEditor _editor;

    public DescriptionEdit(ITasks tasks, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time, IClipboard clipboard,
        IImageFileChooser imageFiles, IFileSystem files, SaveKeys saveKeys, TaskItem task, Action saveRequested, Action cancelRequested)
    {
        _tasks = tasks;
        Started = task;
        Tag = task.Tags.FirstOrDefault();
        _editor = new DescriptionEditor(tasks, contacts, palette, theme, time, clipboard, imageFiles, files, saveKeys) { TaskId = task.Id, Value = task.Description };
        _editor.SaveRequested += (_, _) => saveRequested();
        _editor.CancelRequested += (_, _) => cancelRequested();
    }

    public FrameworkElement Editor => _editor;

    public TaskItem Started { get; }

    public bool ShowsInRow => false;

    public string? Tag { get; set; }

    public bool HasChanges => _editor.HasChanges || EditSaves.TagChanged(Started, Tag);

    public bool Save() => _editor.Save(EditSaves.Description(_tasks, Started, Tag));

    public void FocusEnd() => _editor.FocusEnd();

    public string Draft => _editor.Value;

    public void SetImageWidth(int image, int width) => _editor.SetImageWidth(image, width);

    public void FocusEditor() => _editor.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

    public void Leave(EditCleanup cleanup, Action restore) => cleanup.Leave(Started.Id, HasChanges, restore);

    public void Close(EditCleanup cleanup) => cleanup.Close(Started.Id);
}
