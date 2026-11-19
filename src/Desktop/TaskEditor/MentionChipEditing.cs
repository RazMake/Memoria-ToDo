using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Makes each mention of a text editor one chip: shown as the contact's live text, redrawn when a contact changes while the
/// editor is on screen, and removed whole by Backspace and Delete. The editor's own cursor, click, and selection already treat
/// a chip as one character, and copy takes its Markdown, since the Markdown stays in the document.
/// </summary>
internal sealed class MentionChipEditing
{
    private readonly StyledTextEditor _editor;
    private readonly MentionsInText _mentions;

    public MentionChipEditing(StyledTextEditor editor, MarkdownField field, IContacts contacts, ITheme theme)
    {
        _editor = editor;
        _mentions = new MentionsInText(editor.Document, field);
        var view = editor.TextArea.TextView;
        view.ElementGenerators.Add(new MentionChipGenerator(_mentions, contacts, theme));

        void OnChanged(object? sender, EventArgs e) => OnElementThread.Run(editor, view.Redraw);
        WhileLoaded.Run(editor, () => contacts.Changed += OnChanged, () => contacts.Changed -= OnChanged);
    }

    /// <summary>Adds Backspace and Delete, which remove the whole mention beside the cursor as one undo step.</summary>
    public void AddKeysTo(KeyMap keys) => keys
        .Try(ModifierKeys.None, Key.Back, () => _editor.Run(field => MentionChips.Backspace(field, _mentions.In(field.Text))))
        .Try(ModifierKeys.None, Key.Delete, () => _editor.Run(field => MentionChips.Delete(field, _mentions.In(field.Text))));
}
