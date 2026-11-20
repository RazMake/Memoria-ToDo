namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// What the device controls do: generate, copy and open the key, rename the device, and accept the host
/// key. A problem becomes <see cref="Message"/>, the one message under the controls.
/// </summary>
internal sealed class DeviceActions
{
    public const string NoKeyMessage = "Generate a key first.";

    private readonly SyncUi _ui;
    private readonly Action _changed;

    /// <param name="ui">The window's sync service, setup actions, and operations.</param>
    /// <param name="changed">Called whenever the message is set, so the window shows it.</param>
    public DeviceActions(SyncUi ui, Action changed)
    {
        _ui = ui;
        _changed = changed;
    }

    /// <summary>What to tell the user, or null when there is nothing to say.</summary>
    public string? Message { get; private set; }

    public async Task GenerateKeyAsync()
    {
        Report(null);
        await _ui.RunAsync(
            async token =>
            {
                var setup = _ui.Sync.Setup;
                await setup.GenerateKeyAsync(token);
                if (setup.State.PublicKey is { } key)
                {
                    _ui.Actions.CopyPublicKey(key);
                }
            },
            Report);
    }

    public async Task ForgetKeyAsync()
    {
        Report(null);
        await _ui.RunAsync(token => _ui.Sync.Setup.ForgetKeyAsync(token), Report);
    }

    /// <summary>
    /// Copies the public key without opening a page. Without a key it says to generate one first.
    /// </summary>
    public void CopyPublicKey()
    {
        if (_ui.Sync.Setup.State.PublicKey is not { } key)
        {
            Report(NoKeyMessage);
            return;
        }

        try
        {
            _ui.Actions.CopyPublicKey(key);
            Report(null);
        }
        catch (Exception error)
        {
            _ui.Logger.Error(error, "Copying the public key failed");
            Report(error.Message);
        }
    }

    /// <summary>Saves the trimmed name as the device name, unless it is the name the setup holds already.</summary>
    public async Task SetNameAsync(string name)
    {
        name = name.Trim();
        if (name == _ui.Sync.Setup.State.DeviceName)
        {
            return;
        }

        Report(null);
        await _ui.RunAsync(token => _ui.Sync.Setup.SetDeviceNameAsync(name, token), Report);
    }

    /// <summary>Trusts the new or changed host key that waits to be accepted; does nothing when there is none.</summary>
    public async Task AcceptHostKeyAsync()
    {
        if (_ui.Sync.Setup.State.HostKey is { CanAccept: true } host)
        {
            await _ui.RunAsync(token => _ui.Sync.Setup.AcceptHostKeyAsync(host.Fingerprint, token), Report);
        }
    }

    private void Report(string? text)
    {
        Message = text;
        _changed();
    }
}
