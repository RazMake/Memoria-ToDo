using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Writes the Markdown link for a mention: <c>[text](contact:id "template")</c>. The text is already Markdown.</summary>
internal static class MentionLink
{
    public static string Of(string text, string id, string template) =>
        $"[{text}]({ContactAddress.Of(id)} \"{template.Replace("\\", "\\\\").Replace("\"", "\\\"")}\")";
}
