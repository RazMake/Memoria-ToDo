using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// One shortcut row's setting, as Configure knows it: what the row does, its active shortcut, the message about the
/// last problem, and whether a shortcut field has released the shortcut. It outlives the window, which is made again
/// after each close, and touches no WPF object. Its text lives in the shared settings: <c>readStored</c> reads it and
/// <c>save</c> writes it.
/// </summary>
internal sealed class ShortcutSetting(
    string label, Shortcut defaultShortcut, Func<string?> readStored, Action<string?> save)
{
    private IShortcutAction? _action;
    private string? _message;
    private bool _fromStoredText;
    private bool _released;

    /// <summary>What the shortcut does, such as Add new task.</summary>
    public string Label { get; } = label;

    /// <summary>The shortcut registered for the row, or null when the row has none.</summary>
    public Shortcut? Active { get; private set; }

    /// <summary>What the row says about the last shortcut it couldn't use, or null when there's nothing to say.</summary>
    public string? Message
    {
        get => _message;
        private set
        {
            _message = value;
            _fromStoredText = false;
        }
    }

    /// <summary>
    /// Takes the row's action and registers the stored shortcut, or the default when none is stored or the stored
    /// text isn't valid, at once. Returns whether it registered.
    /// </summary>
    public bool Connect(IShortcutAction action)
    {
        _action = action;
        Active = Stored(out var problem) ?? defaultShortcut;
        ShowStoredProblem(problem);
        Restore();
        return Active is not null;
    }

    /// <summary>
    /// Applies the stored shortcut after the shared settings changed, also while the window is closed. Invalid stored
    /// text is kept as it is: the row keeps its previous shortcut, or the default when it has none, and says why.
    /// Only a message about the stored text is replaced or cleared; a rejected pick's message stays.
    /// While a field has released the shortcut, it only becomes the active one, and <see cref="Restore"/> registers it.
    /// </summary>
    public void SettingsChanged()
    {
        if (_action is null)
        {
            return;
        }

        var stored = Stored(out var problem);
        if (stored is null && Active is not null)
        {
            ShowStoredProblem(problem);
            return;
        }

        stored ??= defaultShortcut;
        if (stored == Active)
        {
            ShowStoredProblem(problem);
        }
        else if (_released)
        {
            Active = stored;
            ShowStoredProblem(problem);
        }
        else if (Register(stored))
        {
            ShowStoredProblem(problem);
        }
    }

    /// <summary>
    /// Checks a pick and makes it the row's shortcut when it follows the rules, differs from the other row's, and
    /// registers; otherwise sets <see cref="Message"/> and keeps the previous shortcut.
    /// </summary>
    public void Pick(ModifierKeys modifiers, Key key, ShortcutSetting other)
    {
        var broken = Shortcut.Check(modifiers, key, out var shortcut);
        if (broken is not null)
        {
            Message = broken;
        }
        else if (shortcut == Active)
        {
            return;
        }
        else if (shortcut == other.Active)
        {
            Message = $"{other.Label} already uses {shortcut!.ToDisplayText()}. Press a different shortcut.";
        }
        else
        {
            var previous = Active;
            if (Register(shortcut!))
            {
                Persist(previous, shortcut!);
            }
        }
    }

    /// <summary>Says that the modifier keys were pressed and released with no other key reaching the field.</summary>
    public void KeySwallowed(ModifierKeys modifiers) =>
        Message = $"If you pressed a key with {ModifierKey.Describe(modifiers)}, Windows or another app uses that " +
            "shortcut. Press a different one.";

    /// <summary>Releases the shortcut so its keys reach the field, and keeps it released after a pick until <see cref="Restore"/>.</summary>
    public void Release()
    {
        _released = true;
        Unregister();
    }

    /// <summary>Registers the shortcut again after <see cref="Release"/>. A row without a shortcut stays without.</summary>
    public void Restore()
    {
        _released = false;
        if (Active is not { } shortcut)
        {
            return;
        }

        try
        {
            if (!Action.Register(shortcut))
            {
                Message = $"Windows or another app already uses {shortcut.ToDisplayText()}, so {Label} has no " +
                    "shortcut. Click the box and press a different one.";
                Active = null;
            }
        }
        catch (Exception error)
        {
            Message = error.Message;
            Active = null;
        }
    }

    private IShortcutAction Action => _action ?? throw new InvalidOperationException("The shortcut isn't connected yet.");

    // Says why the stored text is unusable and remembers that this message is about the stored text. With no problem,
    // it clears only such a message, never one about a pick.
    private void ShowStoredProblem(string? problem)
    {
        if (problem is not null)
        {
            _message = problem;
            _fromStoredText = true;
        }
        else if (_fromStoredText)
        {
            Message = null;
        }
    }

    // The stored shortcut, the default when nothing is stored, or null with the reason when the text is unusable.
    private Shortcut? Stored(out string? problem)
    {
        problem = null;
        try
        {
            var text = readStored();
            if (text is null)
            {
                return defaultShortcut;
            }

            var shortcut = Shortcut.Parse(text);
            problem = shortcut is null ? $"The saved {Label} shortcut \"{text}\" isn't a valid shortcut." : null;
            return shortcut;
        }
        catch (Exception error)
        {
            problem = $"The saved {Label} shortcut couldn't be read: {error.Message}";
            return null;
        }
    }

    // Saves a registered pick. When saving fails, the row goes back to its previous shortcut and says what failed,
    // including a failure to register the previous one again.
    private void Persist(Shortcut? previous, Shortcut shortcut)
    {
        try
        {
            save(shortcut.ToDisplayText());
        }
        catch (Exception error)
        {
            Active = previous;
            Message = $"{shortcut.ToDisplayText()} wasn't saved for {Label}: {error.Message}";
            if (!_released)
            {
                Reregister(previous);
            }
        }
    }

    private void Reregister(Shortcut? previous)
    {
        try
        {
            if (previous is null)
            {
                Action.Unregister();
            }
            else if (!Action.Register(previous))
            {
                Message += $" {previous.ToDisplayText()} couldn't be registered again, so {Label} has no shortcut.";
                Active = null;
            }
        }
        catch (Exception error)
        {
            Message += $" {Label}'s previous shortcut couldn't be restored: {error.Message}";
            Active = null;
        }
    }

    private bool Register(Shortcut shortcut)
    {
        try
        {
            if (!Action.Register(shortcut))
            {
                Message = $"Windows or another app already uses {shortcut.ToDisplayText()}. Press a different shortcut.";
                return false;
            }
        }
        catch (Exception error)
        {
            Message = error.Message;
            return false;
        }

        Active = shortcut;
        Message = null;
        if (_released)
        {
            Unregister();
        }

        return true;
    }

    private void Unregister()
    {
        try
        {
            Action.Unregister();
        }
        catch (Exception error)
        {
            Message = error.Message;
        }
    }
}
