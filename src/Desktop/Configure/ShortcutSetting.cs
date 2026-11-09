using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// One shortcut row's setting, as Configure knows it: what the row does, its active shortcut, the message about the
/// last problem, and whether a shortcut field has released the shortcut. It outlives the window, which is made again
/// after each close, and touches no WPF object.
/// </summary>
internal sealed class ShortcutSetting(string label, Shortcut defaultShortcut)
{
    private IShortcutAction? _action;
    private bool _released;

    /// <summary>What the shortcut does, such as Add new task.</summary>
    public string Label { get; } = label;

    /// <summary>The shortcut registered for the row, or null when the row has none.</summary>
    public Shortcut? Active { get; private set; }

    /// <summary>What the row says about the last shortcut it couldn't use, or null when there's nothing to say.</summary>
    public string? Message { get; private set; }

    /// <summary>Takes the row's action and registers the default at once. Returns whether it registered.</summary>
    public bool Connect(IShortcutAction action)
    {
        _action = action;
        Active = defaultShortcut;
        Message = null;
        Restore();
        return Active is not null;
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
            Register(shortcut!);
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

    private void Register(Shortcut shortcut)
    {
        try
        {
            if (!Action.Register(shortcut))
            {
                Message = $"Windows or another app already uses {shortcut.ToDisplayText()}. Press a different shortcut.";
                return;
            }
        }
        catch (Exception error)
        {
            Message = error.Message;
            return;
        }

        Active = shortcut;
        Message = null;
        if (_released)
        {
            Unregister();
        }
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
