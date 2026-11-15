using System.Numerics;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// A global shortcut: modifier keys and one other key. Only the two defaults and <see cref="TryCreate"/> make
/// one, so every shortcut follows the rules: two or more of Ctrl, Alt, Shift, and Win, plus one other key, and never F12.
/// </summary>
public sealed record Shortcut
{
    private const ModifierKeys AllModifiers =
        ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows;

    private Shortcut(ModifierKeys modifiers, Key key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    /// <summary>Win+Alt+N, the shortcut Add new task starts with.</summary>
    internal static Shortcut AddNewTaskDefault { get; } = new(ModifierKeys.Windows | ModifierKeys.Alt, Key.N);

    /// <summary>Win+Alt+V, the shortcut Manage tasks starts with.</summary>
    internal static Shortcut ManageTasksDefault { get; } = new(ModifierKeys.Windows | ModifierKeys.Alt, Key.V);

    /// <summary>Shift+Esc, the shortcut Close window starts with.</summary>
    internal static Shortcut CloseWindowDefault { get; } = new(ModifierKeys.Shift, Key.Escape);

    /// <summary>
    /// Makes the Close window shortcut and returns null, or returns why the keys break its rules and no shortcut: two
    /// or more modifier keys plus any other key, or one modifier key plus Esc or a function key (F1 to F24).
    /// </summary>
    internal static string? CheckCloseWindow(ModifierKeys modifiers, Key key, out Shortcut? shortcut)
    {
        shortcut = null;
        if (CheckKeys(modifiers, key) is { } broken)
        {
            return broken;
        }

        var count = BitOperations.PopCount((uint)modifiers);
        if (count < 2 && (count == 0 || key != Key.Escape && key is < Key.F1 or > Key.F24))
        {
            return "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus any key, or one modifier key plus Esc or a function key.";
        }

        shortcut = new Shortcut(modifiers, key);
        return null;
    }

    /// <summary>Reads text that <see cref="ToDisplayText"/> wrote and that follows <see cref="CheckCloseWindow"/>; null for any other text.</summary>
    internal static Shortcut? ParseCloseWindow(string? text) => Parse(text, CheckCloseWindow);

    /// <summary>The modifier keys held with <see cref="Key"/>.</summary>
    public ModifierKeys Modifiers { get; }

    /// <summary>The key that isn't a modifier.</summary>
    public Key Key { get; }

    /// <summary>Makes the shortcut when the keys follow the rules; otherwise returns false and no shortcut.</summary>
    public static bool TryCreate(ModifierKeys modifiers, Key key, out Shortcut? shortcut) =>
        Check(modifiers, key, out shortcut) is null;

    /// <summary>Makes the shortcut and returns null, or returns why the keys break the rules and no shortcut.</summary>
    internal static string? Check(ModifierKeys modifiers, Key key, out Shortcut? shortcut)
    {
        shortcut = null;
        if (CheckKeys(modifiers, key) is { } broken)
        {
            return broken;
        }

        if (key == Key.F12)
        {
            return "F12 is kept by Windows for debuggers.";
        }

        if (BitOperations.PopCount((uint)modifiers) < 2)
        {
            return "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus one other key.";
        }

        shortcut = new Shortcut(modifiers, key);
        return null;
    }

    // The rules every shortcut follows: real modifier keys, and a real key that isn't a modifier.
    private static string? CheckKeys(ModifierKeys modifiers, Key key)
    {
        if ((modifiers & ~AllModifiers) != ModifierKeys.None)
        {
            return "Use only Ctrl, Alt, Shift, and Win as modifier keys.";
        }

        if (!Enum.IsDefined(key) || key is Key.System or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            return "Pick a key on the keyboard.";
        }

        if (key == Key.None || ModifierKey.Of(key) != ModifierKeys.None)
        {
            return "Add a key besides the modifier keys.";
        }

        return null;
    }

    /// <summary>The shortcut as the user reads it, such as <c>Win+Alt+T</c>.</summary>
    internal string ToDisplayText() => $"{ModifierKey.Describe(Modifiers)}+{Key}";

    /// <summary>
    /// Reads text that <see cref="ToDisplayText"/> wrote, and also follows the rules of <see cref="Check"/>. Returns
    /// null for any other text, including the same keys in a different spelling or order.
    /// </summary>
    internal static Shortcut? Parse(string? text) => Parse(text, Check);

    private delegate string? Rules(ModifierKeys modifiers, Key key, out Shortcut? shortcut);

    private static Shortcut? Parse(string? text, Rules rules)
    {
        var parts = text?.Split('+');
        if (parts is not { Length: > 1 } || !Enum.TryParse<Key>(parts[^1], out var key))
        {
            return null;
        }

        var modifiers = ModifierKeys.None;
        foreach (var flag in new[] { ModifierKeys.Windows, ModifierKeys.Control, ModifierKeys.Alt, ModifierKeys.Shift })
        {
            if (parts[..^1].Contains(ModifierKey.Describe(flag)))
            {
                modifiers |= flag;
            }
        }

        return rules(modifiers, key, out var shortcut) is null && shortcut!.ToDisplayText() == text ? shortcut : null;
    }
}
