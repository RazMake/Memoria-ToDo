using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// A shortcut that works only inside the app's windows, so it is never registered with Windows: its keys, and the
/// message about the last problem. <c>apply</c> hands the active keys to the app's windows, at creation and after each
/// change. Its text lives in the shared settings: <c>readStored</c> reads it and <c>save</c> writes it. Close window
/// and Save differ only in their label, default, and rules.
/// </summary>
internal abstract class WindowKeysSetting : IShortcutSetting
{
    private readonly Shortcut _default;
    private readonly Func<string?> _readStored;
    private readonly Action<string?> _save;
    private readonly Action<ModifierKeys, Key> _apply;
    private readonly RowMessage _message = new();

    protected WindowKeysSetting(
        string label, Shortcut defaultShortcut, Func<string?> readStored, Action<string?> save,
        Action<ModifierKeys, Key> apply)
    {
        ArgumentNullException.ThrowIfNull(readStored);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(apply);
        Label = label;
        _default = defaultShortcut;
        _readStored = readStored;
        _save = save;
        _apply = apply;
        var stored = Read(out var problem) ?? _default;
        _message.SayAboutStoredText(problem);
        Use(stored);
    }

    public string Label { get; }

    /// <summary>The keys of the shortcut; the default when nothing valid is stored.</summary>
    public Shortcut? Active { get; private set; }

    /// <summary>What the row says about the last problem, or null when there's nothing to say.</summary>
    public string? Message => _message.Text;

    /// <summary>Makes the shortcut when the keys follow the row's rules, otherwise returns why they don't.</summary>
    protected abstract string? Check(ModifierKeys modifiers, Key key, out Shortcut? shortcut);

    /// <summary>Reads stored text that follows the row's rules; null for any other text.</summary>
    protected abstract Shortcut? Parse(string? text);

    /// <summary>Applies the stored keys after the shared settings changed, also while the window is closed.</summary>
    public void SettingsChanged()
    {
        var stored = Read(out var problem);
        _message.SayAboutStoredText(problem);
        if (stored is not null && stored != Active)
        {
            Use(stored);
        }
    }

    /// <summary>
    /// Checks a pick and makes it the keys when it follows the row's rules and differs from every other row's;
    /// otherwise sets <see cref="Message"/> and keeps the previous keys.
    /// </summary>
    public void Pick(ModifierKeys modifiers, Key key, params IUsedShortcut[] others)
    {
        var broken = Check(modifiers, key, out var shortcut);
        if (broken is not null)
        {
            _message.Say(broken);
            return;
        }

        if (shortcut == Active)
        {
            return;
        }

        if (StoredShortcut.UsedBy(shortcut!, others) is { } used)
        {
            _message.Say(used);
            return;
        }

        try
        {
            _save(shortcut!.ToDisplayText());
        }
        catch (Exception error)
        {
            _message.Say($"{shortcut!.ToDisplayText()} wasn't saved for {Label}: {error.Message}");
            return;
        }

        _message.Say(null);
        Use(shortcut);
    }

    // The stored keys, the default when nothing is stored, or null with the reason when the text is unusable.
    private Shortcut? Read(out string? problem) => StoredShortcut.Read(Label, _default, _readStored, Parse, out problem);

    private void Use(Shortcut shortcut)
    {
        Active = shortcut;
        _apply(shortcut.Modifiers, shortcut.Key);
    }
}
