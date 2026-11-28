using Microsoft.Win32;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The signed-in user's light or dark setting, from Windows.</summary>
/// <remarks>
/// Making it reads and watches nothing. It watches Windows from the first <see cref="MayHaveChanged"/> handler added
/// until the last one is removed.
/// </remarks>
public sealed class WindowsThemeSetting : IThemeSetting
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private const string SystemUsesLightThemeValue = "SystemUsesLightTheme";

    private readonly object _gate = new();
    private EventHandler? _handlers;

    /// <inheritdoc />
    /// <remarks>
    /// Reads as WPF's system theme does: the apps' value, else the system's, else dark. A value that isn't a number
    /// counts as missing. 0 is dark.
    /// </remarks>
    public bool ReadIsDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        var light = key?.GetValue(AppsUseLightThemeValue) as int?
            ?? key?.GetValue(SystemUsesLightThemeValue) as int?
            ?? 0;
        return light == 0;
    }

    /// <inheritdoc />
    public event EventHandler? MayHaveChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_gate)
            {
                if (_handlers is null)
                {
                    SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; // Throws before any state changes.
                }

                _handlers += value;
            }
        }
        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_gate)
            {
                if (_handlers is null)
                {
                    return;
                }

                _handlers -= value;
                if (_handlers is null)
                {
                    SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                }
            }
        }
    }

    // Runs on the system events thread, which tells only the handlers added at that moment.
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        EventHandler? handlers;
        lock (_gate)
        {
            handlers = _handlers;
        }

        handlers?.Invoke(this, EventArgs.Empty);
    }
}
