using System.Windows.Input;

namespace Memoria.ToDo.Desktop;

/// <summary>Holds Alt for an action, through <see cref="HeldModifier"/>. Shared test setup, not a fake.</summary>
internal static class HeldAlt
{
    /// <inheritdoc cref="HeldModifier.While"/>
    public static void While(Action action) => HeldModifier.While(ModifierKeys.Alt, action);
}
