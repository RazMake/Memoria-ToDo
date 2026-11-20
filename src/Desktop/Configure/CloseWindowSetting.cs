using System.Windows.Input;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Close window shortcut: Shift+Esc at first, and the rules of <see cref="Shortcut.CheckCloseWindow"/>.</summary>
internal sealed class CloseWindowSetting(
    Func<string?> readStored, Action<string?> save, Action<ModifierKeys, Key> apply, ILogger logger)
    : WindowKeysSetting("Close window", Shortcut.CloseWindowDefault, readStored, save, apply, logger)
{
    protected override string? Check(ModifierKeys modifiers, Key key, out Shortcut? shortcut) =>
        Shortcut.CheckCloseWindow(modifiers, key, out shortcut);

    protected override Shortcut? Parse(string? text) => Shortcut.ParseCloseWindow(text);
}
