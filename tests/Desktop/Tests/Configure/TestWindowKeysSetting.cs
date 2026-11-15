using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// A shortcut that only works inside the app's windows (Close window or Save), wired to an in-memory stored text, for
/// unit tests. Shared test setup, not a fake of a port. Set <see cref="Stored"/>, <see cref="ReadFailure"/>, or
/// <see cref="SaveFailure"/> before <see cref="Create"/> or later; <see cref="Saved"/> lists the texts saved and
/// <see cref="Applied"/> the keys handed to the app's windows.
/// </summary>
internal abstract class TestWindowKeysSetting
{
    public string? Stored { get; set; }
    public Exception? ReadFailure { get; set; }
    public Exception? SaveFailure { get; set; }
    public List<string?> Saved { get; } = [];
    public List<(ModifierKeys Modifiers, Key Key)> Applied { get; } = [];

    /// <summary>The setting, as the shortcut rows use it.</summary>
    public abstract IShortcutSetting Keys { get; }

    /// <summary>Tells the setting the shared settings changed.</summary>
    public abstract void SettingsChanged();

    /// <summary>Makes the setting, as Configure does when it is created, with the stored text as it is now.</summary>
    public abstract void Create();

    protected string? ReadStored() => ReadFailure is null ? Stored : throw ReadFailure;

    protected void Save(string? text)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        Saved.Add(text);
        Stored = text;
    }

    protected void Apply(ModifierKeys modifiers, Key key) => Applied.Add((modifiers, key));
}
