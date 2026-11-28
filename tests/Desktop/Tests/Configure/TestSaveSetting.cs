using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>The Save setting wired to an in-memory stored text, for unit tests; see <see cref="TestWindowKeysSetting"/>.</summary>
internal sealed class TestSaveSetting : TestWindowKeysSetting
{
    /// <summary>The Save rule's message for no modifier key, as the design words it.</summary>
    public const string NoModifierMessage = "Use one or more modifier keys (Ctrl, Alt, Shift, Win) plus any key.";

    /// <summary>The Save rule's message for Shift alone with a key that types a character.</summary>
    public const string ShiftAloneMessage =
        "Shift alone with a key that types a character would stop it typing. Add Ctrl, Alt, or Win.";

    public static Shortcut Default => Make(ModifierKeys.Control, Key.Enter);

    public SaveSetting Setting { get; private set; } = null!;

    public override IShortcutSetting Keys => Setting;

    public override void SettingsChanged() => Setting.SettingsChanged();

    public override void Create() => Setting = new SaveSetting(ReadStored, Save, Apply, Serilog.Core.Logger.None);

    /// <summary>Makes a Save shortcut through its rules.</summary>
    public static Shortcut Make(ModifierKeys modifiers, Key key) =>
        Shortcut.CheckSave(modifiers, key, out var shortcut) is null
            ? shortcut!
            : throw new ArgumentException($"{modifiers}+{key} isn't a valid Save shortcut.");
}
