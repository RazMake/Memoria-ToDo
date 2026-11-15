using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Close window setting wired to an in-memory stored text, for unit tests. Shared test setup, not a fake of a
/// port. Set <see cref="Stored"/>, <see cref="ReadFailure"/>, or <see cref="SaveFailure"/> before <see cref="Create"/> or
/// later; <see cref="Saved"/> lists the texts saved and <see cref="Applied"/> the keys handed to the app's windows.
/// </summary>
internal sealed class TestCloseWindowSetting
{
    /// <summary>The Close window rule's message, as the design words it.</summary>
    public const string RuleMessage =
        "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus any key, or one modifier key plus Esc or a function key.";

    public static Shortcut Default => Make(ModifierKeys.Shift, Key.Escape);

    public CloseWindowSetting Setting { get; private set; } = null!;

    public string? Stored { get; set; }
    public Exception? ReadFailure { get; set; }
    public Exception? SaveFailure { get; set; }
    public List<string?> Saved { get; } = [];
    public List<(ModifierKeys Modifiers, Key Key)> Applied { get; } = [];

    /// <summary>Makes the setting, as Configure does when it is created, with the stored text as it is now.</summary>
    public TestCloseWindowSetting Create()
    {
        Setting = new CloseWindowSetting(ReadStored, Save, (modifiers, key) => Applied.Add((modifiers, key)));
        return this;
    }

    /// <summary>Makes a Close window shortcut through its rules.</summary>
    public static Shortcut Make(ModifierKeys modifiers, Key key) =>
        Shortcut.CheckCloseWindow(modifiers, key, out var shortcut) is null
            ? shortcut!
            : throw new ArgumentException($"{modifiers}+{key} isn't a valid Close window shortcut.");

    private string? ReadStored() => ReadFailure is null ? Stored : throw ReadFailure;

    private void Save(string? text)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        Saved.Add(text);
        Stored = text;
    }
}
