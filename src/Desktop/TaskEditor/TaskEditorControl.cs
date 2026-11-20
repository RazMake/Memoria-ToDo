using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The Title, Description, and one tag of a task, with every key the Task editor's design lists.</summary>
internal sealed class TaskEditorControl : UserControl, IDraftEditor
{
    private readonly TitleField _title;
    private readonly DescriptionField _description;
    private readonly TagsModel _tagsModel;
    private readonly TagEditor _tags;
    private readonly KeyRouter _router = new();
    private readonly KeyMap _editorKeys = new();
    private readonly PickerLists _lists;
    private readonly ImageInsertion _images;
    private readonly EditorBaseline _baseline = new();
    private readonly EditorSave _save;
    private readonly MessageLine _message = new();
    private string _taskId = "";

    public TaskEditorControl(ITasks tasks, ITagColors tagColors, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, SaveKeys saveKeys, ILogger logger)
        : this(new TagsModel(tasks, tagColors, logger), tasks, tagColors, Argument.Required(contacts), palette, theme, time, clipboard, imageFiles, files, Argument.Required(saveKeys), logger)
    {
    }

    // The model is made in the first constructor's arguments, so a missing tasks or tagColors is rejected before any control exists.
    private TaskEditorControl(TagsModel tagsModel, ITasks tasks, ITagColors tagColors, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme,
        TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, SaveKeys saveKeys, ILogger logger)
    {
        _tagsModel = tagsModel;
        _tags = new TagEditor(tasks, _tagsModel, new TagLook(tagColors, theme));
        _lists = new PickerLists(tasks, contacts, palette, theme);
        _images = new ImageInsertion(tasks, clipboard, imageFiles, files, () => _taskId, _message.Show, logger);
        var snippets = new SnippetInserter(time, clipboard, _lists.TaskReferences.Open, _images.InsertClipboardOrChosen);
        _title = new TitleField(theme, _lists, snippets, _router);
        _description = new DescriptionField(theme, _lists, snippets, _images, _router);
        _save = new EditorSave(_tagsModel.AddTypedText, () => Value, _title.MarkMissing, FocusTitle, _message.Show, logger);

        var panel = new StackPanel { Margin = new Thickness(16) };
        Add(panel, "Title", _title.Editor);
        Add(panel, "Description", _description.Editor);
        _lists.AddTo(panel);
        Add(panel, "Tag", _tags);
        panel.Children.Add(_message);
        Content = panel;
        FocusVisualStyle = null;

        _editorKeys
            .OnSaveKeys(saveKeys, () => RequestSave(SaveRequested))
            .On(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter, () => RequestSave(SaveAndClearRequested))
            .On(ModifierKeys.None, Key.Escape, () =>
            {
                if (!CloseOpenList())
                {
                    CancelRequested?.Invoke(this, EventArgs.Empty);
                }
            });
        _title.Keys
            .On(ModifierKeys.None, Key.Enter, () => Focus(_description.Editor))
            .On(ModifierKeys.None, Key.Tab, () => Focus(_description.Editor));
        ListKeys.AddTo(_description.Keys, _description.Editor, _tags.FocusFirst, FocusTitle);

        _router.Listen(this, _editorKeys);
    }

    /// <summary>Makes the keys that work across the editor work while <paramref name="element"/>, outside it, has the focus.</summary>
    internal void ShareKeysWith(UIElement element) => _router.Listen(element, _editorKeys);

    /// <summary>Asks to save, as the Save keys do: typed tag text becomes a tag first.</summary>
    internal void RequestSave() => RequestSave(SaveRequested);

    /// <summary>What the fields hold now. Setting it replaces all three fields; a task saved with several tags shows its first, so <see cref="Value"/> then holds only it.</summary>
    public TaskDraft Value
    {
        get => new(_taskId, _title.Editor.Text, _description.Editor.Text, _tagsModel.Tags);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _taskId = value.TaskId;
            _title.Editor.Text = value.Title;
            _description.Editor.Text = value.Description;
            _tagsModel.Tags = [.. value.Tags.Take(1)];
            _baseline.Reset(Value);
        }
    }

    /// <summary>
    /// Whether anything changed since <see cref="Value"/> was last set, or since the editor was made. Tags are compared
    /// as Tasks' edit compares them: ignoring case and order. Non-blank tag text typed and not yet added counts too.
    /// </summary>
    public bool HasChanges => _baseline.HasChanges(_title.Editor.Text, _description.Editor.Text, _tagsModel.Tags, _tagsModel.TypedText);

    /// <summary>The Tags text typed and not yet added as a tag. It isn't part of <see cref="Value"/>, and setting <see cref="Value"/> clears it.</summary>
    string IDraftEditor.TypedTags
    {
        get => _tagsModel.TypedText;
        set => _tagsModel.TypedText = value;
    }

    /// <summary>The Save keys.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Ctrl+Shift+Enter.</summary>
    public event EventHandler? SaveAndClearRequested;

    /// <summary>Esc, with no list open.</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Puts the cursor in the Title.</summary>
    public void FocusTitle() => Focus(_title.Editor);

    /// <summary>Puts the cursor at the end of the Description.</summary>
    public void FocusDescription() => _description.Editor.FocusEnd();

    /// <summary>
    /// Adds typed tag text as a tag, then calls <paramref name="save"/> with <see cref="Value"/>, unless the Title is
    /// empty, which it marks and puts the cursor in. Returns false, keeping the text, when the Title is empty or
    /// <paramref name="save"/> threw a failure Tasks' saves are known to throw; the editor says why. Other failures propagate.
    /// </summary>
    public bool Save(Action<TaskDraft> save) => _save.Run(save);

    /// <summary>Shows the text under the Tags line, or hides the line when the text is empty.</summary>
    void IDraftEditor.ShowMessage(string text) => _message.Show(text);

    /// <summary>Closes the open list or tag part: colors, snippets, tasks, the tag suggestions, the + menu, or Custom. Returns false, doing nothing, when none was open.</summary>
    internal bool CloseOpenList() => _lists.Popups.CloseOpen() || _tags.CloseEdit();

    private static void Add(Panel panel, string caption, UIElement field)
    {
        panel.Children.Add(new TextBlock { Text = caption, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(field);
    }

    private static void Focus(UIElement field) => KeyboardFocus.MoveTo(field);

    // Typed tag text becomes a tag first, so saving loses none of what the user typed.
    private void RequestSave(EventHandler? requested)
    {
        if (!_tagsModel.AddTypedText())
        {
            return;
        }

        requested?.Invoke(this, EventArgs.Empty);
    }
}
