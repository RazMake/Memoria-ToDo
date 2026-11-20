using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Close window setting wired to an in-memory stored text, for unit tests; see <see cref="TestWindowKeysSetting"/>.
/// </summary>
internal sealed class TestCloseWindowSetting : TestWindowKeysSetting
{
    /// <summary>The Close window rule's message, as the design words it.</summary>
    public const string RuleMessage =
        "Use two or more modifier keys (Ctrl, Alt, Shift, Win) plus any key, or one modifier key plus Esc or a function key.";

    public static Shortcut Default => Make(ModifierKeys.Shift, Key.Escape);

    public CloseWindowSetting Setting { get; private set; } = null!;

    public override IShortcutSetting Keys => Setting;

    public override void SettingsChanged() => Setting.SettingsChanged();

    public override void Create() => Setting = new CloseWindowSetting(ReadStored, Save, Apply, Serilog.Core.Logger.None);

    /// <summary>Makes a Close window shortcut through its rules.</summary>
    public static Shortcut Make(ModifierKeys modifiers, Key key) =>
        Shortcut.CheckCloseWindow(modifiers, key, out var shortcut) is null
            ? shortcut!
            : throw new ArgumentException($"{modifiers}+{key} isn't a valid Close window shortcut.");
}
