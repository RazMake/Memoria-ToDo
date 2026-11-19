using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The Description text field and its keys, as every editor that edits a description has them: styled in place, growing
/// with its content, with formatting, heading, color, snippet, task reference, and image keys, the "/" snippet picker,
/// and image drops. The editor adds the list keys, which depend on where the cursor goes when it leaves the field.
/// </summary>
internal sealed class DescriptionField
{
    // The field grows with its content between these heights, then scrolls. The cap is proposed in the Task editor's design.
    private const double MinHeight = 140;
    private const double MaxHeight = 400;

    public DescriptionField(ITheme theme, PickerLists lists, SnippetInserter snippets, ImageInsertion images, KeyRouter router)
    {
        Editor = new StyledTextEditor("Description", MarkdownField.Description, theme, lists.Contacts)
        {
            Margin = new Thickness(0, 0, 0, 12),
            MinHeight = MinHeight,
            MaxHeight = MaxHeight,
            WordWrap = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _ = new SnippetPicking(Editor, lists.SnippetChoices, lists.Popups, snippets);
        ImageDrop.AddTo(Editor, images);
        lists.TaskReferences.Watch(Editor);
        lists.PickMentionsIn(Editor);
        Editor.Chips.AddKeysTo(Keys);
        FormattingKeys.AddInline(Keys, Editor, () => lists.Colors.Open(Editor));
        FormattingKeys.AddHeadings(Keys, Editor);
        SnippetKeys.AddTo(Keys, Editor, isTitle: false, snippets);
        Keys.Try(ModifierKeys.Control, Key.V, () => images.PasteClipboardImage(Editor));
        router.Route(Editor, lists.Keys, Keys);
    }

    public StyledTextEditor Editor { get; }

    /// <summary>The keys of the field, for the editor to add its own to.</summary>
    public KeyMap Keys { get; } = new();
}
