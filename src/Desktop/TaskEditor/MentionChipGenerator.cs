using ICSharpCode.AvalonEdit.Rendering;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Shows each mention of a text editor as one Formatted view <see cref="MentionChip"/>, in place of its Markdown, which stays in the
/// document: the contact's live text, or the stored text when Contacts has none. The chip doesn't follow the theme, so the editor's
/// redraw on a theme change, like one on a contact change, makes new chips.
/// </summary>
internal sealed class MentionChipGenerator : VisualLineElementGenerator
{
    private readonly MentionsInText _mentions;
    private readonly IContacts _contacts;
    private readonly ITheme _theme;

    public MentionChipGenerator(MentionsInText mentions, IContacts contacts, ITheme theme)
    {
        _mentions = mentions;
        _contacts = contacts;
        _theme = theme;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        foreach (var mention in _mentions.Chips)
        {
            if (mention.Start >= startOffset)
            {
                return mention.Start < CurrentContext.VisualLine.LastDocumentLine.EndOffset ? mention.Start : -1;
            }
        }

        return -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var mention = _mentions.Chips.FirstOrDefault(chip => chip.Start == offset);
        return mention is null
            ? null
            : new InlineObjectElement(mention.Length, new MentionChip(MentionChips.TextOf(_contacts, mention), _theme.IsDark));
    }
}
