using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The rule for text typed or pasted into the tag box: lowercase, with no whitespace.</summary>
internal static class TagText
{
    /// <summary>The text in lowercase by the invariant culture's rules, with every whitespace character removed.</summary>
    public static string Of(string text) =>
        string.Concat(text.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();

    /// <summary>A paste handler that swaps the pasted text for <see cref="Of"/> of it.</summary>
    public static void OnPaste(object sender, DataObjectPastingEventArgs e) => TextPaste.Rewrite(e, Of);
}
