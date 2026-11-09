using System.Windows.Input;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The keys that run Editing's formatting commands, as the Task editor's design lists them.</summary>
internal static class FormattingKeys
{
    /// <summary>Registers the commands that work in both text fields. <paramref name="openColors"/> runs for Ctrl+Shift+C.</summary>
    public static void AddInline(KeyMap keys, StyledTextEditor field, Action openColors)
    {
        Command(keys, field, ModifierKeys.Control, Key.B, Formatting.Bold);
        Command(keys, field, ModifierKeys.Control, Key.I, Formatting.Italic);
        Command(keys, field, ModifierKeys.Control | ModifierKeys.Shift, Key.X, Formatting.Strikethrough);
        Command(keys, field, ModifierKeys.Control, Key.E, Formatting.InlineCode);
        keys.On(ModifierKeys.Control | ModifierKeys.Shift, Key.C, openColors);
    }

    /// <summary>Registers Ctrl+1, Ctrl+2, and Ctrl+3, which set the heading level of the line.</summary>
    public static void AddHeadings(KeyMap keys, StyledTextEditor field)
    {
        Command(keys, field, ModifierKeys.Control, Key.D1, text => Formatting.Heading(text, 1));
        Command(keys, field, ModifierKeys.Control, Key.D2, text => Formatting.Heading(text, 2));
        Command(keys, field, ModifierKeys.Control, Key.D3, text => Formatting.Heading(text, 3));
    }

    private static void Command(KeyMap keys, StyledTextEditor field, ModifierKeys modifiers, Key key,
        Func<TextAndSelection, TextAndSelection?> command) =>
        keys.Try(modifiers, key, () => field.Run(command));
}
