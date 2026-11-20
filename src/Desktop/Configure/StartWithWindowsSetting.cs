using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Start with Windows decision behind its check box: whether it is on, and why a switch failed.</summary>
internal sealed class StartWithWindowsSetting
{
    private readonly IStartWithWindows _startWithWindows;
    private readonly ILogger _logger;

    /// <param name="logger">Where a failed switch is logged.</param>
    public StartWithWindowsSetting(IStartWithWindows startWithWindows, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        ArgumentNullException.ThrowIfNull(logger);
        _startWithWindows = startWithWindows;
        _logger = logger;
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
            _logger.Error(error, "Switching Start with Windows {State} failed", on ? "on" : "off");
            Problem = error.Message;
        }
    }
}
