using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The Title text field and its keys, as every editor that edits a title has them: one line, styled in place, with
/// formatting, color, snippet, and task reference keys. The editor adds the keys that move on from it.
/// </summary>
internal sealed class TitleField
{
    private const string MissingStatus = "The title is empty. Type a title to save.";

    public TitleField(ITheme theme, PickerLists lists, SnippetInserter snippets, KeyRouter router, IPointerPosition pointer, Action<string> report, Serilog.ILogger logger)
    {
        Editor = new StyledTextEditor("Title", MarkdownField.Title, theme, lists.Contacts, pointer, logger)
        {
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        lists.TaskReferences.Watch(Editor);
        lists.PickMentionsIn(Editor, report);
        Editor.Chips.AddKeysTo(Keys);
        Keys.On(ModifierKeys.Shift, Key.Enter, () => { }); // The Title is one line, so no key breaks it.
        FormattingKeys.AddInline(Keys, Editor, () => lists.Colors.Open(Editor));
        SnippetKeys.AddTo(Keys, Editor, isTitle: true, snippets);
        router.Route(Editor, lists.Keys, Keys);
        Editor.TextArea.TextEntering += (_, e) =>
        {
            if (e.Text.Contains('\r') || e.Text.Contains('\n'))
            {
                e.Handled = true;
            }
        };
        Editor.TextChanged += (_, _) => ShowMissing(false);
        DataObject.AddPastingHandler(Editor.TextArea, TextPaste.JoinLines);
    }

    public StyledTextEditor Editor { get; }

    /// <summary>The keys of the field, for the editor to add its own to.</summary>
    public KeyMap Keys { get; } = new();

    /// <summary>Marks the field as empty, for the eye (a red border) and for assistive tools, until it is edited.</summary>
    public void MarkMissing() => ShowMissing(true);

    private void ShowMissing(bool missing)
    {
        if (missing)
        {
            AutomationProperties.SetItemStatus(Editor, MissingStatus);
        }
        else
        {
            Editor.ClearValue(AutomationProperties.ItemStatusProperty);
        }

        Editor.ShowInvalid(missing);
    }
}
