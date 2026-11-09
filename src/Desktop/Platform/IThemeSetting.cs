namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Windows' light or dark setting, read the way WPF reads it to pick the windows' theme.</summary>
public interface IThemeSetting
{
    /// <summary>Reads the setting now: true when windows show the dark theme.</summary>
    bool ReadIsDark();

    /// <summary>Raised, on any thread, when Windows may have changed the setting; reading tells whether it did.</summary>
    event EventHandler? MayHaveChanged;
}
