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
        if ((modifiers & ~AllModifiers) != ModifierKeys.None)
        {
            return "Use only Ctrl, Alt, Shift, and Win as modifier keys.";
        }

        if (!Enum.IsDefined(key) || key is Key.System or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            return "Pick a key on the keyboard.";
        }

        if (key == Key.F12)
        {
            return "F12 is kept by Windows for debuggers.";
        }

        if (key == Key.None || ModifierKey.Of(key) != ModifierKeys.None)
        {
            return "Add a key besides the modifier keys.";
        }

        if (BitOperations.PopCount((uint)modifiers) < 2)
        {
            return "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus one other key.";
        }

        shortcut = new Shortcut(modifiers, key);
        return null;
    }

    /// <summary>The shortcut as the user reads it, such as <c>Win+Alt+T</c>.</summary>
    internal string ToDisplayText() => $"{ModifierKey.Describe(Modifiers)}+{Key}";

    /// <summary>
    /// Reads text that <see cref="ToDisplayText"/> wrote, and also follows the rules of <see cref="Check"/>. Returns
    /// null for any other text, including the same keys in a different spelling or order.
    /// </summary>
    internal static Shortcut? Parse(string? text)
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

        return Check(modifiers, key, out var shortcut) is null && shortcut!.ToDisplayText() == text ? shortcut : null;
    }
}
