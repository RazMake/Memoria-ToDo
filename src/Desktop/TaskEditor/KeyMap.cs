using System.Windows.Input;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Runs the action registered for a key with the modifiers held, so key handlers need no switch or if chain.</summary>
internal sealed class KeyMap
{
    private readonly Dictionary<(ModifierKeys, Key), Func<bool>> _actions = [];

    /// <summary>Registers the action for the chord. Fails when the chord already has one.</summary>
    public KeyMap On(ModifierKeys modifiers, Key key, Action action) =>
        Try(modifiers, key, () =>
        {
            action();
            return true;
        });

    /// <summary>
    /// Registers an action that returns whether it applied, for a key that keeps its usual meaning when it doesn't.
    /// Fails when the chord already has one.
    /// </summary>
    public KeyMap Try(ModifierKeys modifiers, Key key, Func<bool> action)
    {
        _actions.Add((modifiers, key), action);
        return this;
    }

    /// <summary>Runs the chord's action. Returns false when the chord has none, or its action didn't apply.</summary>
    public bool TryRun(ModifierKeys modifiers, Key key) =>
        _actions.TryGetValue((modifiers, key), out var action) && action();
}
