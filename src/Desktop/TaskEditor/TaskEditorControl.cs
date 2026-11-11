using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The Title, Description, and Tags of one task, with every key the Task editor's design lists.</summary>
public sealed class TaskEditorControl : UserControl
{
    private const string MissingTitleStatus = "The title is empty. Type a title to save.";

    // The Description grows with its content between these heights, then scrolls. The cap is proposed in the Task editor's design.
    private const double DescriptionMinHeight = 140;
    private const double DescriptionMaxHeight = 400;

    private readonly StyledTextEditor _title;
    private readonly StyledTextEditor _description;
    private readonly TagsLine _tags;
    private readonly HeldModifierKeys _held = new();
    private readonly KeyMap _editorKeys = new();
    private readonly KeyMap _titleKeys = new();
    private readonly KeyMap _descriptionKeys = new();
    private readonly KeyMap _listKeys = new();
    private readonly PopupList _colors = new("ColorList");
    private readonly PopupLists _popups;
    private readonly IReadOnlyList<string> _colorNames;
    private readonly ImageInsertion _images;
    private readonly TextBlock _message = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Firebrick,
        Margin = new Thickness(0, 0, 0, 8),
        Visibility = Visibility.Collapsed,
    };
    private StyledTextEditor? _colorTarget;
    private string _taskId = "";
    private string _baselineTitle = "";
    private string _baselineDescription = "";
    private HashSet<string> _baselineTags = new(StringComparer.OrdinalIgnoreCase);

    public TaskEditorControl(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files)
    {
        _tags = new TagsLine(tasks);
        _title = Field("Title", MarkdownField.Title, palette, theme);
        _title.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _title.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _description = Field("Description", MarkdownField.Description, palette, theme);
        _description.MinHeight = DescriptionMinHeight;
        _description.MaxHeight = DescriptionMaxHeight;
        _description.WordWrap = true;
        _description.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _description.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _colorNames = [.. palette.Select(color => color.Name)];
        var snippetList = new PopupList("SnippetList");
        var taskList = new PopupList("TaskReferenceList");
        _popups = new PopupLists(_colors, snippetList, taskList);
        var taskReferences = new TaskReferencePicking(tasks, taskList, _popups);
        var images = new ImageInsertion(tasks, clipboard, imageFiles, files, () => _taskId,
            ShowMessage);
        _images = images;
        var snippets = new SnippetInserter(time, clipboard, taskReferences.Open, images.InsertClipboardOrChosen);
        _ = new SnippetPicking(_description, snippetList, _popups, snippets);
        ImageDrop.AddTo(_description, images);
        taskReferences.Watch(_title);
        taskReferences.Watch(_description);

        var panel = new StackPanel { Margin = new Thickness(16) };
        Add(panel, "Title", _title);
        Add(panel, "Description", _description);
        panel.Children.Add(_colors);
        panel.Children.Add(snippetList);
        panel.Children.Add(taskList);
        Add(panel, "Tags", _tags);
        panel.Children.Add(_message);
        Content = panel;

        _editorKeys
            .On(ModifierKeys.Control, Key.Enter, () => RequestSave(SaveRequested))
            .On(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter, () => RequestSave(SaveAndClearRequested))
            .On(ModifierKeys.None, Key.Escape, () =>
            {
                if (!CloseOpenList())
                {
                    CancelRequested?.Invoke(this, EventArgs.Empty);
                }
            });
        _listKeys
            .Try(ModifierKeys.None, Key.Down, _popups.MoveDown)
            .Try(ModifierKeys.None, Key.Enter, _popups.ChooseMovedOrClose);
        _titleKeys
            .On(ModifierKeys.None, Key.Enter, () => Focus(_description))
            .On(ModifierKeys.None, Key.Tab, () => Focus(_description))
            .On(ModifierKeys.Shift, Key.Enter, () => { }); // The Title is one line, so no key breaks it.
        FormattingKeys.AddInline(_titleKeys, _title, () => OpenColors(_title));
        FormattingKeys.AddInline(_descriptionKeys, _description, () => OpenColors(_description));
        FormattingKeys.AddHeadings(_descriptionKeys, _description);
        SnippetKeys.AddTo(_titleKeys, _title, isTitle: true, snippets);
        SnippetKeys.AddTo(_descriptionKeys, _description, isTitle: false, snippets);
        ListKeys.AddTo(_descriptionKeys, _description, _tags.FocusInput, () => Focus(_title));
        _descriptionKeys.Try(ModifierKeys.Control, Key.V, () => images.PasteClipboardImage(_description));
        _colors.Chosen += (_, position) => ApplyColor(_colorNames[position]);

        _title.PreviewKeyDown += (_, e) => Handle(_listKeys, _titleKeys, e);
        _description.PreviewKeyDown += (_, e) => Handle(_listKeys, _descriptionKeys, e);
        _title.TextChanged += (_, _) =>
        {
            _title.ClearValue(AutomationProperties.ItemStatusProperty);
            _title.ShowInvalid(false);
        };
        DataObject.AddPastingHandler(_title.TextArea, TextPaste.JoinLines);

        Listen(this);
    }

    /// <summary>Makes the keys that work across the editor work while <paramref name="element"/>, outside it, has the focus.</summary>
    internal void ShareKeysWith(UIElement element) => Listen(element);

    /// <summary>Asks to save, as Ctrl+Enter does: typed tag text becomes a tag first.</summary>
    internal void RequestSave() => RequestSave(SaveRequested);

    private void Listen(UIElement element)
    {
        element.PreviewKeyDown += OnPreviewKeyDown;
        element.PreviewKeyUp += (_, e) => _held.Released(EventKey.Of(e));
        element.IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (!(bool)e.NewValue)
            {
                _held.Clear();
            }
        };
    }

    /// <summary>What the fields hold now. Setting it replaces all three fields.</summary>
    public TaskDraft Value
    {
        get => new(_taskId, _title.Text, _description.Text, _tags.Tags);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _taskId = value.TaskId;
            _title.Text = value.Title;
            _description.Text = value.Description;
            _tags.Tags = value.Tags;
            _baselineTitle = _title.Text;
            _baselineDescription = _description.Text;
            _baselineTags = new HashSet<string>(_tags.Tags, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Whether anything changed since <see cref="Value"/> was last set, or since the editor was made. Tags are compared
    /// as Tasks' edit compares them: ignoring case and order. Non-blank tag text typed and not yet added counts too.
    /// </summary>
    public bool HasChanges =>
        _title.Text != _baselineTitle
        || _description.Text != _baselineDescription
        || !_baselineTags.SetEquals(_tags.Tags)
        || !string.IsNullOrWhiteSpace(_tags.TypedText);

    /// <summary>The Tags text typed and not yet added as a tag. It isn't part of <see cref="Value"/>, and setting <see cref="Value"/> clears it.</summary>
    internal string TypedTags
    {
        get => _tags.TypedText;
        set => _tags.TypedText = value;
    }

    /// <summary>Ctrl+Enter.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Ctrl+Shift+Enter.</summary>
    public event EventHandler? SaveAndClearRequested;

    /// <summary>Esc, with no list open.</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Puts the cursor in the Title.</summary>
    public void FocusTitle() => Focus(_title);

    /// <summary>
    /// Adds typed tag text as a tag, then calls <paramref name="save"/> with <see cref="Value"/>, unless the Title is
    /// empty, which it marks and puts the cursor in. Returns false, keeping the text, when the Title is empty or
    /// <paramref name="save"/> threw a failure Tasks' saves are known to throw; the editor says why. Other failures propagate.
    /// </summary>
    public bool Save(Action<TaskDraft> save)
    {
        ArgumentNullException.ThrowIfNull(save);
        _tags.AddTypedText();
        var draft = Value;
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            MarkTitleMissing();
            FocusTitle();
            return false;
        }

        try
        {
            save(draft);
        }
        catch (Exception e) when (SaveFailures.MessageOf(e) is not null)
        {
            ShowMessage(SaveFailures.MessageOf(e)!);
            return false;
        }

        ShowMessage("");
        return true;
    }

    /// <summary>Shows the text under the Tags line, or hides the line when the text is empty.</summary>
    internal void ShowMessage(string text)
    {
        _message.Text = text;
        _message.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Whether the image file dialog is open.</summary>
    internal bool IsChoosingImageFile => _images.IsChoosingFile;

    /// <summary>Closes the open list: colors, snippets, tasks, or the tag suggestions. Returns false, doing nothing, when none was open.</summary>
    internal bool CloseOpenList() => _popups.CloseOpen() || _tags.CloseSuggestions();

    /// <summary>Marks the Title as empty, for the eye (a red border) and for assistive tools, until it is edited.</summary>
    private void MarkTitleMissing()
    {
        AutomationProperties.SetItemStatus(_title, MissingTitleStatus);
        _title.ShowInvalid(true);
    }

    private static StyledTextEditor Field(string automationId, MarkdownField field, IReadOnlyList<PaletteColor> palette, ITheme theme) =>
        new(automationId, field, palette, theme) { Margin = new Thickness(0, 0, 0, 12) };

    private static void Add(Panel panel, string caption, UIElement field)
    {
        panel.Children.Add(new TextBlock { Text = caption, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(field);
    }

    private static void Focus(UIElement field) => KeyboardFocus.MoveTo(field);

    // Typed tag text becomes a tag first, so saving loses none of what the user typed.
    private void RequestSave(EventHandler? requested)
    {
        _tags.AddTypedText();
        requested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = EventKey.Of(e);
        if (ModifierKey.Of(key) != ModifierKeys.None)
        {
            _held.Pressed(key);
            return;
        }

        _held.OtherKeyReached();
        Handle(_editorKeys, e);
    }

    private void OpenColors(StyledTextEditor field)
    {
        _colorTarget = field;
        _popups.Show(_colors, _colorNames);
    }

    private void ApplyColor(string name)
    {
        var field = _colorTarget ?? throw new InvalidOperationException("A color was chosen with no color list open.");
        _colorTarget = null;
        _colors.Close();
        field.Run(text => Formatting.Color(text, name, _colorNames));
        Focus(field);
    }

    private void Handle(KeyMap first, KeyMap then, KeyEventArgs e)
    {
        Handle(first, e);
        Handle(then, e);
    }

    private void Handle(KeyMap keys, KeyEventArgs e)
    {
        if (!e.Handled && keys.TryRun(_held.Modifiers, EventKey.Of(e)))
        {
            e.Handled = true;
        }
    }
}
