using System.Windows.Input;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Test data for the rules every Configure shortcut shares, Close window's and Save's included: which keys count as
/// real keys and modifiers, and which text is not how a shortcut is written. One home for the cases.
/// </summary>
public static class ShortcutKeyData
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys Alt = ModifierKeys.Alt;

    /// <summary>Keys and modifiers that aren't real, with the message a rule gives for them.</summary>
    public static TheoryData<ModifierKeys, Key, string> NotRealKeys { get; } = new()
    {
        { Ctrl | Alt, Key.LeftCtrl, "Add a key besides the modifier keys." },
        { Ctrl, Key.RWin, "Add a key besides the modifier keys." },
        { Ctrl | Alt, Key.None, "Add a key besides the modifier keys." },
        { Ctrl | Alt, Key.System, "Pick a key on the keyboard." },
        { Ctrl | Alt, Key.ImeProcessed, "Pick a key on the keyboard." },
        { Ctrl | Alt, Key.DeadCharProcessed, "Pick a key on the keyboard." },
        { Ctrl | Alt, (Key)int.MaxValue, "Pick a key on the keyboard." },
        { (ModifierKeys)0x10 | Ctrl | Alt, Key.K, "Use only Ctrl, Alt, Shift, and Win as modifier keys." },
    };

    /// <summary>Text that isn't a shortcut as Configure writes it, whatever the row's rules.</summary>
    public static TheoryData<string?> NotShortcutText { get; } = new(
        [null, "", "Escape", "Shift+Esc", "Escape+Shift", "Shift+escape", "not a shortcut"]);
}
