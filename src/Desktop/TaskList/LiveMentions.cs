using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>Shows a title's or description's mentions with Contacts' current text, as Task View shows them.</summary>
internal static class LiveMentions
{
    /// <summary>The Markdown with each mention's text swapped for Contacts' live text; a mention Contacts has no text for keeps its stored text.</summary>
    public static string Of(IContacts contacts, string markdown, bool title) =>
        Mentions.WithTexts(markdown, title, mention => contacts.TextOf(mention.Address, mention.Template));
}
