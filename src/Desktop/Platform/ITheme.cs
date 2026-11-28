namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Whether the app's windows show the light or the dark theme, which follows Windows' setting.</summary>
public interface ITheme
{
    /// <summary>True while windows show the dark theme.</summary>
    bool IsDark { get; }

    /// <summary>Raised on the UI thread after Windows switches between light and dark.</summary>
    event EventHandler? Changed;
}
