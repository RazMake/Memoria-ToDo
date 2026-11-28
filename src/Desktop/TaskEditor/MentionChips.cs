using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The rules of a mention chip: what it shows, and how Backspace and Delete remove it whole. The editor's chip element makes the cursor,
/// a click, and a selection treat it as one character. Offsets are Editing's text, with "\n" for every line break.
/// </summary>
internal static class MentionChips
{
    /// <summary>The chip's text: the contact's live text, or the stored text when Contacts has none.</summary>
    public static string TextOf(IContacts contacts, Mention mention) =>
        contacts.TextOf(mention.Address, mention.Template) ?? mention.StoredText;

    /// <summary>The mention whose chip covers the text offset, or null when none does. A chip covers its text from Start up to, not including, its end.</summary>
    public static Mention? At(IReadOnlyList<Mention> mentions, int offset) =>
        mentions.FirstOrDefault(mention => mention.Start <= offset && offset < mention.Start + mention.Length);

    /// <summary>Backspace with the cursor right after a chip and nothing selected: the text without that mention. Null when it doesn't apply.</summary>
    public static TextAndSelection? Backspace(TextAndSelection field, IReadOnlyList<Mention> mentions) =>
        Without(field, mentions.FirstOrDefault(mention => mention.Start + mention.Length == field.SelectionStart));

    /// <summary>Delete with the cursor right before a chip and nothing selected: the text without that mention. Null when it doesn't apply.</summary>
    public static TextAndSelection? Delete(TextAndSelection field, IReadOnlyList<Mention> mentions) =>
        Without(field, mentions.FirstOrDefault(mention => mention.Start == field.SelectionStart));

    private static TextAndSelection? Without(TextAndSelection field, Mention? mention) =>
        mention is null || field.SelectionLength != 0
            ? null
            : new TextAndSelection(field.Text.Remove(mention.Start, mention.Length), mention.Start, 0);
}
