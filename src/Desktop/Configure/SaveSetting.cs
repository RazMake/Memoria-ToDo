using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Save shortcut: Ctrl+Enter at first, and the rules of <see cref="Shortcut.CheckSave"/>.</summary>
internal sealed class SaveSetting(Func<string?> readStored, Action<string?> save, Action<ModifierKeys, Key> apply)
    : WindowKeysSetting("Save", Shortcut.SaveDefault, readStored, save, apply)
{
    protected override string? Check(ModifierKeys modifiers, Key key, out Shortcut? shortcut) =>
        Shortcut.CheckSave(modifiers, key, out shortcut);

    protected override Shortcut? Parse(string? text) => Shortcut.ParseSave(text);
}
