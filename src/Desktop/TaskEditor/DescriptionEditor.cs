using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>One task's description, edited alone, as Add's Description edits it, images included.</summary>
public sealed class DescriptionEditor : UserControl
{
    private readonly DescriptionField _description;
    private readonly SingleFieldEditing _editing;
    private string _taskId = "";

    public DescriptionEditor(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, SaveKeys saveKeys)
        : this(tasks, new PickerLists(tasks, palette), Argument.Required(saveKeys), theme, time, clipboard, imageFiles, files)
    {
    }

    // The lists are made in the first constructor's arguments, so a missing tasks or palette is rejected before any control exists.
    private DescriptionEditor(ITasks tasks, PickerLists lists, SaveKeys saveKeys, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files)
    {
        var router = new KeyRouter();
        var message = new MessageLine();
        var images = new ImageInsertion(tasks, clipboard, imageFiles, files, () => _taskId, message.Show);
        var snippets = new SnippetInserter(time, clipboard, lists.TaskReferences.Open, images.InsertClipboardOrChosen);
        _description = new DescriptionField(theme, lists, snippets, images, router);
        var editor = _description.Editor;
        ListKeys.AddTo(_description.Keys, editor, () => Leave(FocusNavigationDirection.Next), () => Leave(FocusNavigationDirection.Previous));
        _editing = new SingleFieldEditing(this, editor, lists, message, saveKeys, router,
            () => SaveRequested?.Invoke(this, EventArgs.Empty), () => CancelRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>The task the description belongs to; an added image is kept for it.</summary>
    public string TaskId
    {
        get => _taskId;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _taskId = value;
        }
    }

    /// <summary>The description's Markdown. Setting it replaces the text and starts <see cref="HasChanges"/> over.</summary>
    public string Value
    {
        get => _editing.Value;
        set => _editing.Value = value;
    }

    /// <summary>Whether the text differs at all from <see cref="Value"/> as last set.</summary>
    public bool HasChanges => _editing.HasChanges;

    /// <summary>
    /// Calls <paramref name="save"/> with <see cref="Value"/>. Returns false, keeping the text, when save threw a failure
    /// Tasks' saves are known to throw; the editor says why under the field. Other failures propagate.
    /// </summary>
    public bool Save(Action<string> save) => _editing.Save(save);

    /// <summary>
    /// Sets image number <paramref name="image"/>, as the description page numbers them, to show <paramref name="width"/>
    /// pixels wide, as a typed change: <see cref="HasChanges"/> compares it, and Ctrl+Z undoes it in one step.
    /// Throws ArgumentOutOfRangeException, changing nothing, as <see cref="ImageWidths.With"/> does.
    /// </summary>
    public void SetImageWidth(int image, int width) =>
        _description.Editor.Run(current => new TextAndSelection(ImageWidths.With(current.Text, image, width), current.SelectionStart, current.SelectionLength));

    /// <summary>Puts the cursor at the end of the description.</summary>
    public void FocusEnd() => _description.Editor.FocusEnd();

    /// <summary>The Save shortcut.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Esc, with no list open.</summary>
    public event EventHandler? CancelRequested;

    // Tab outside a list item moves on to the next control, as it does in any other field that doesn't take it.
    private void Leave(FocusNavigationDirection direction) => _description.Editor.MoveFocus(new TraversalRequest(direction));
}
