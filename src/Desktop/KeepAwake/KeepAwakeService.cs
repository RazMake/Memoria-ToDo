using System.ComponentModel;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.KeepAwake;

/// <summary>
/// Keeps Windows' display request in line with the setting and the lock state: held while the setting is on and the session unlocked.
/// Changes run on the thread the caller or the session's event arrives on, which is the UI thread, and raise <see cref="Changed"/> there.
/// </summary>
internal sealed class KeepAwakeService(IDisplayRequest display, ISessionLock session, OnOffSettingFile setting, ILogger logger) : IKeepAwake
{
    private bool _running;
    private bool _on;
    private bool _locked;
    private bool _held;
    private string? _settingFailure;
    private string? _requestFailure;

    public event EventHandler? Changed;

    public KeepAwakeState State => new(_on, _on && _locked, _settingFailure ?? _requestFailure);

    public void Start()
    {
        if (_running)
        {
            throw new InvalidOperationException("Keep awake has started already.");
        }

        Change(() =>
        {
            (_on, _settingFailure) = setting.Read();
            _locked = ReadLocked();
            logger.Debug("Keep awake starting: the setting is {Setting} and the session is {Lock}", _on ? "on" : "off", _locked ? "locked" : "unlocked");
            _running = true;
            session.Changed += OnSessionChanged;
            Apply("the start");
        });
    }

    public void SetOn(bool on)
    {
        setting.Write(on);
        Change(() =>
        {
            _on = on;
            _settingFailure = null;
            logger.Information("Keep awake turned {State}", on ? "on" : "off");
            if (_running)
            {
                Apply("the setting");
            }
        });
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        session.Changed -= OnSessionChanged;
        if (_held || _requestFailure is not null)
        {
            SetRequest(false, "the stop");
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Change(() =>
    {
        _locked = ReadLocked();
        Apply(_locked ? "a lock" : "an unlock");
    });

    /// <summary>The lock state; unlocked, with a Warning, when Windows can't say.</summary>
    private bool ReadLocked()
    {
        try
        {
            return session.IsLocked;
        }
        catch (Win32Exception e)
        {
            logger.Warning(e, "Keep awake can't tell whether the session is locked, so it counts as unlocked");
            return false;
        }
    }

    /// <summary>
    /// Holds the request when the setting is on and the session unlocked, and clears it otherwise. A refusal shows in <see cref="State"/>;
    /// after one, the next call asks Windows again even when the wanted state is the one last held, as the refused change may be half done.
    /// </summary>
    private void Apply(string reason)
    {
        var wanted = _on && !_locked;
        if (wanted == _held && _requestFailure is null)
        {
            return;
        }

        try
        {
            SetRequest(wanted, reason);
            _requestFailure = null;
        }
        catch (Win32Exception e)
        {
            _requestFailure = e.Message;
        }
    }

    /// <summary>Asks Windows to set or clear the request. Logs a refusal at Error and rethrows it.</summary>
    private void SetRequest(bool on, string reason)
    {
        try
        {
            display.Set(on);
        }
        catch (Win32Exception e)
        {
            logger.Error(e, "Keep awake: Windows refused to {Action} the request, because of {Reason}: {Error}", on ? "set" : "clear", reason, e.Message);
            throw;
        }

        _held = on;
        logger.Information("Keep awake {Action} the request, because of {Reason}", on ? "set" : "cleared", reason);
    }

    private void Change(Action mutate)
    {
        var before = State;
        mutate();
        if (before != State)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
