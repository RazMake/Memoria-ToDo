using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>One task's title, edited alone, as Add's Title edits it. Enter saves, since the title is one line.</summary>
public sealed class TitleEditor : UserControl
{
    private readonly TitleField _title;
    private readonly SingleFieldEditing _editing;

    public TitleEditor(ITasks tasks, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, SaveKeys saveKeys, IPointerPosition pointer, ILogger logger)
        : this(Argument.Required(pointer), new PickerLists(tasks, contacts, palette, theme, logger), Argument.Required(saveKeys), theme, time, clipboard, Argument.Required(logger))
    {
    }

    // The lists are made in the first constructor's arguments, so a missing tasks or palette is rejected before any control exists.
    private TitleEditor(IPointerPosition pointer, PickerLists lists, SaveKeys saveKeys, ITheme theme, TimeProvider time, IClipboard clipboard, ILogger logger)
    {
        var router = new KeyRouter();
        var message = new MessageLine();
        var snippets = new SnippetInserter(time, clipboard, lists.TaskReferences.Open, NoImages);
        _title = new TitleField(theme, lists, snippets, router, pointer, message.Show, logger);
        _title.Keys.On(ModifierKeys.None, Key.Enter, RequestSave);
        var panel = new StackPanel();
        panel.Children.Add(_title.Editor);
        lists.AddTo(panel);
        panel.Children.Add(message);
        Content = panel;
        _editing = new SingleFieldEditing(this, _title.Editor, lists, message, saveKeys, router, RequestSave,
            () => CancelRequested?.Invoke(this, EventArgs.Empty), logger);
    }

    /// <summary>The title's Markdown. Setting it replaces the text and starts <see cref="HasChanges"/> over.</summary>
    public string Value
    {
        get => _editing.Value;
        set => _editing.Value = value;
    }

    /// <summary>Whether the text differs at all from <see cref="Value"/> as last set.</summary>
    public bool HasChanges => _editing.HasChanges;

    /// <summary>
    /// Calls <paramref name="save"/> with <see cref="Value"/>, unless it's empty, which it marks. Returns false, keeping the
    /// text, when it's empty or save threw a failure Tasks' saves are known to throw; the editor says why under the field.
    /// Other failures propagate.
    /// </summary>
    public bool Save(Action<string> save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (string.IsNullOrWhiteSpace(Value))
        {
            _title.MarkMissing();
            return false;
        }

        return _editing.Save(save);
    }

    /// <summary>Puts the cursor at the end of the title.</summary>
    public void FocusEnd() => _title.Editor.FocusEnd();

    /// <summary>The Save shortcut, or Enter.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Esc, with no list open.</summary>
    public event EventHandler? CancelRequested;

    private void RequestSave() => SaveRequested?.Invoke(this, EventArgs.Empty);

    // The Title has no image snippet, so no key reaches this.
    private static void NoImages(ITextField field, ISnippet snippet, int? replaceFrom) =>
        throw new NotSupportedException("A Title holds no images.");
}
