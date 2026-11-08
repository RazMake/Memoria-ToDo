using System.Windows.Input;
using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>Makes shortcuts for tests, through the rules every real shortcut passes. Shared test setup, not a fake of a port.</summary>
public static class TestShortcut
{
    /// <summary>Ctrl+Alt+K, a valid shortcut that isn't the default.</summary>
    public static Shortcut A { get; } = Of(ModifierKeys.Control | ModifierKeys.Alt, Key.K);

    /// <summary>Ctrl+Shift+J, a valid shortcut that is neither the default nor <see cref="A"/>.</summary>
    public static Shortcut B { get; } = Of(ModifierKeys.Control | ModifierKeys.Shift, Key.J);

    /// <summary>Win+Alt+N, the shortcut Add new task starts with.</summary>
    public static Shortcut AddNewTaskDefault { get; } = Of(ModifierKeys.Windows | ModifierKeys.Alt, Key.N);

    /// <summary>Win+Alt+V, the shortcut Manage tasks starts with.</summary>
    public static Shortcut ManageTasksDefault { get; } = Of(ModifierKeys.Windows | ModifierKeys.Alt, Key.V);

    /// <summary>Makes a shortcut.</summary>
    /// <exception cref="ArgumentException">The keys don't follow the rules.</exception>
    public static Shortcut Of(ModifierKeys modifiers, Key key) =>
        Shortcut.TryCreate(modifiers, key, out var shortcut)
            ? shortcut!
            : throw new ArgumentException($"{modifiers}+{key} isn't a valid shortcut.");
}
