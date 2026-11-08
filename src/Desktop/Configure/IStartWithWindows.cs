namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Start with Windows setting, which is the current user's <c>Run</c> registry entry. The composition root implements it.</summary>
public interface IStartWithWindows
{
    /// <summary>Whether the <c>Run</c> key holds the app's entry.</summary>
    bool IsOn();

    /// <summary>Writes or removes the entry.</summary>
    /// <exception cref="InvalidOperationException">The app isn't installed, or Windows refused the write.</exception>
    void Set(bool on);
}
