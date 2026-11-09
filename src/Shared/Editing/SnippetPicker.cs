namespace Memoria.ToDo.Shared.Editing;

/// <summary>Typing "/" in the Description: when the picker opens, what it lists, and what choosing a snippet does.</summary>
public static class SnippetPicker
{
    /// <summary>True when the character at <paramref name="slashIndex"/> is a "/" at a line start or after a space.</summary>
    public static bool OpensAt(string text, int slashIndex) => throw new NotImplementedException();

    /// <summary>The snippets that match the text between the "/" and the cursor.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slashIndex"/> is not a "/" before the cursor.</exception>
    public static IReadOnlyList<ISnippet> Matching(TextAndSelection field, int slashIndex) => throw new NotImplementedException();

    /// <summary>Inserts <paramref name="snippet"/> in place of the "/" and the text typed after it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slashIndex"/> is not a "/" before the cursor.</exception>
    public static TextAndSelection Insert(TextAndSelection field, int slashIndex, ISnippet snippet, SnippetInput input) => throw new NotImplementedException();
}
