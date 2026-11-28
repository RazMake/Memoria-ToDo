using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Reads the key of a keyboard event the way a window sees it.</summary>
public static class EventKey
{
    /// <summary>The key pressed. Alt makes WPF report <see cref="Key.System"/>, with the real key in <see cref="KeyEventArgs.SystemKey"/>; this returns that real key.</summary>
    public static Key Of(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e.Key == Key.System ? e.SystemKey : e.Key;
    }
}
