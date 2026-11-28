using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// One shortcut row's setting, as Configure knows it: what the row does, its active shortcut, the message about the
/// last problem, and whether a shortcut field has released the shortcut. It outlives the window, which is made again
/// after each close, and touches no WPF object. Its text lives in the shared settings: <c>readStored</c> reads it and
/// <c>save</c> writes it. A failure it catches and shows is logged at Error to <c>logger</c>.
/// </summary>
internal sealed class ShortcutSetting(
    string label, Shortcut defaultShortcut, Func<string?> readStored, Action<string?> save, ILogger logger) : IShortcutSetting
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private IShortcutAction? _action;
    private readonly RowMessage _message = new();
    private bool _released;

    /// <summary>What the shortcut does, such as Add new task.</summary>
    public string Label { get; } = label;

    /// <summary>The shortcut registered for the row, or null when the row has none.</summary>
    public Shortcut? Active { get; private set; }

    /// <summary>What the row says about the last shortcut it couldn't use, or null when there's nothing to say.</summary>
    public string? Message
    {
        get => _message.Text;
        private set => _message.Say(value);
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
    public void Pick(ModifierKeys modifiers, Key key, params IUsedShortcut[] others)
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
        else if (StoredShortcut.UsedBy(shortcut!, others) is { } used)
        {
            Message = used;
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
        RegisterActive();
    }

    /// <summary>
    /// Registers the shortcut again through its action, as when Configure is shown, unless a field has released it.
    /// An action that still holds the shortcut answers true at once, so nothing changes then.
    /// </summary>
    public void RegisterAgain()
    {
        if (!_released)
        {
            RegisterActive();
        }
    }

    private void RegisterActive()
    {
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
            _logger.Error(error, "Registering the {Label} shortcut {Shortcut} failed", Label, shortcut.ToDisplayText());
            Message = error.Message;
            Active = null;
        }
    }

    private IShortcutAction Action => _action ?? throw new InvalidOperationException("The shortcut isn't connected yet.");

    // Says why the stored text is unusable and remembers that this message is about the stored text. With no problem,
    // it clears only such a message, never one about a pick.
    private void ShowStoredProblem(string? problem) => _message.SayAboutStoredText(problem);

    // The stored shortcut, the default when nothing is stored, or null with the reason when the text is unusable.
    private Shortcut? Stored(out string? problem) =>
        StoredShortcut.Read(Label, defaultShortcut, readStored, Shortcut.Parse, _logger, out problem);

    // Saves a registered pick. When saving fails, the row goes back to its previous shortcut and says what failed,
    // including a failure to register the previous one again. A save kept before a notice failed keeps the pick, with the shared message.
    private void Persist(Shortcut? previous, Shortcut shortcut)
    {
        var saved = StoreFailures.Run(
            () => save(shortcut.ToDisplayText()),
            error => $"{shortcut.ToDisplayText()} wasn't saved for {Label}: {error.Message}",
            message => Message = message,
            _logger);
        if (!saved)
        {
            Active = previous;
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
            _logger.Error(error, "Restoring the previous {Label} shortcut failed", Label);
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
            _logger.Error(error, "Registering the {Label} shortcut {Shortcut} failed", Label, shortcut.ToDisplayText());
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
            _logger.Error(error, "Releasing the {Label} shortcut failed", Label);
            Message = error.Message;
        }
    }
}
