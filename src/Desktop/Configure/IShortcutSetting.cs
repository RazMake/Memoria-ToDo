using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>A shortcut row's setting as its row shows it: its keys, the message under its field, and the picks the user makes.</summary>
internal interface IShortcutSetting : IUsedShortcut
{
    /// <summary>What the row says about the last problem, or null when there's nothing to say.</summary>
    string? Message { get; }

    /// <summary>Checks a pick against the row's rules and the other rows' shortcuts, and uses it when it passes.</summary>
    void Pick(ModifierKeys modifiers, Key key, params IUsedShortcut[] others);
}
