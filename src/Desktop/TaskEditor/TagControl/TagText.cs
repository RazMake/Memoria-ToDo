using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The rule for the text of the New tag box, applied to the whole text after every change.</summary>
internal static class TagText
{
    /// <summary>
    /// The text as <see cref="TagName.Normalize"/> spells the tag, except that whitespace at the end stays one space, so
    /// typing <c>to do</c> shows <c>to </c> until the next character turns the space into a hyphen. Whitespace at the start
    /// does nothing, and text of only whitespace becomes empty.
    /// </summary>
    public static string Of(string text)
    {
        var tag = TagName.Normalize(text);
        return tag.Length > 0 && char.IsWhiteSpace(text[^1]) ? tag + " " : tag;
    }
}
