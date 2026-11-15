namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Start with Windows decision behind its check box: whether it is on, and why a switch failed.</summary>
internal sealed class StartWithWindowsSetting
{
    private readonly IStartWithWindows _startWithWindows;

    public StartWithWindowsSetting(IStartWithWindows startWithWindows)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        _startWithWindows = startWithWindows;
        IsOn = startWithWindows.IsOn();
    }

    /// <summary>Whether the app starts with Windows: the current setting, or the last switch that succeeded.</summary>
    public bool IsOn { get; private set; }

    /// <summary>Why the last switch failed, or null when it succeeded.</summary>
    public string? Problem { get; private set; }

    /// <summary>Switches the setting. A failure keeps <see cref="IsOn"/> as it was and sets <see cref="Problem"/>.</summary>
    public void Set(bool on)
    {
        try
        {
            _startWithWindows.Set(on);
            IsOn = on;
            Problem = null;
        }
        catch (Exception error)
        {
            Problem = error.Message;
        }
    }
}
