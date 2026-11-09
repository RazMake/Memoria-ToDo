using System.Windows.Input;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The keys that insert Editing's snippets, as the Task editor's design lists them.</summary>
internal static class SnippetKeys
{
    private const ModifierKeys CtrlShift = ModifierKeys.Control | ModifierKeys.Shift;

    private static readonly (ModifierKeys Modifiers, Key Key, ISnippet Snippet)[] Chords =
    [
        (ModifierKeys.Control, Key.OemSemicolon, Snippets.DateStamp),
        (CtrlShift, Key.OemSemicolon, Snippets.DateAndTimeStamp),
        (CtrlShift, Key.N, Snippets.Note),
        (CtrlShift, Key.T, Snippets.Tip),
        (CtrlShift, Key.W, Snippets.Warning),
        (ModifierKeys.Control, Key.K, Snippets.Link),
        (CtrlShift, Key.I, Snippets.Image),
        (CtrlShift, Key.E, Snippets.CodeBlock),
        (CtrlShift, Key.R, Snippets.TaskReference),
        (CtrlShift, Key.G, Snippets.Table),
        (CtrlShift, Key.OemMinus, Snippets.HorizontalLine),
    ];

    /// <summary>Registers the snippet keys that work in the field. The Title gets only the snippets that work in a Title.</summary>
    public static void AddTo(KeyMap keys, StyledTextEditor field, bool isTitle, SnippetInserter inserter)
    {
        foreach (var (modifiers, key, snippet) in Chords)
        {
            if (!isTitle || snippet.WorksInTitle)
            {
                keys.On(modifiers, key, () => inserter.Insert(field, snippet, null));
            }
        }
    }
}
