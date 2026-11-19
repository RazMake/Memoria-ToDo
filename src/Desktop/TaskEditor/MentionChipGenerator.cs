using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit.Rendering;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Shows each mention of a text editor as one chip, in place of its Markdown, which stays in the document: the contact's live text
/// on the theme's mention background, in the normal text color.
/// </summary>
internal sealed class MentionChipGenerator : VisualLineElementGenerator
{
    // The mention background of the light look, then of the dark one.
    private static readonly Brush[] Backgrounds = [TagLook.Paint(TextLooks.Light.MentionBackground), TagLook.Paint(TextLooks.Dark.MentionBackground)];

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
            : new Chip(MentionChips.TextOf(_contacts, mention), mention.Length, Backgrounds[_theme.IsDark ? 1 : 0]);
    }

    // The background is set when the text is made, after the line transformers have had their say.
    private sealed class Chip(string text, int documentLength, Brush background) : FormattedTextElement(text, documentLength)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            TextRunProperties.SetBackgroundBrush(background);
            return base.CreateTextRun(startVisualColumn, context);
        }
    }
}
