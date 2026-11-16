using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Runs the action registered for a key with the modifiers held, so key handlers need no switch or if chain.</summary>
internal sealed class KeyMap
{
    private readonly Dictionary<(ModifierKeys, Key), Func<bool>> _actions = [];
        private SaveKeys? _saveKeys;
        private Action? _save;
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

    /// <summary>
    /// Registers the action for whatever keys <paramref name="saveKeys"/> holds when a key is pressed, so a new Save
    /// shortcut applies at once. The Save keys win over a chord registered with <see cref="On"/> or <see cref="Try"/>.
    /// </summary>
    public KeyMap OnSaveKeys(SaveKeys saveKeys, Action action)
    {
        ArgumentNullException.ThrowIfNull(saveKeys);
        ArgumentNullException.ThrowIfNull(action);
        if (_saveKeys is not null)
        {
            throw new InvalidOperationException("The Save keys already have an action.");
        }

        _saveKeys = saveKeys;
        _save = action;
        return this;
    }

    /// <summary>Runs the chord's action. Returns false when the chord has none, or its action didn't apply.</summary>
    public bool TryRun(ModifierKeys modifiers, Key key)
    {
        if (_saveKeys?.Matches(modifiers, key) == true)
        {
            _save!();
            return true;
        }

        return _actions.TryGetValue((modifiers, key), out var action) && action();
    }
}
