namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// What the device controls do: generate, copy and open the key, rename the device, and accept or forget the host
/// key. A problem becomes <see cref="Message"/>, the one message under the controls.
/// </summary>
internal sealed class DeviceActions
{
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

    public Task GenerateKeyAsync() => _ui.RunAsync(token => _ui.Sync.Setup.GenerateKeyAsync(token), Report);

    /// <summary>
    /// Copies the public key, then opens the host's page for adding it. Without a key it opens nothing; a failed copy
    /// opens no page; a host with no page leaves the key copied and says where to add it.
    /// </summary>
    public void CopyPublicKey()
    {
        var state = _ui.Sync.Setup.State;
        if (state.PublicKey is not { } key)
        {
            Report("Generate a key first.");
            return;
        }

        try
        {
            _ui.Actions.CopyPublicKey(key);
            if (state.AddKeyPage is { } page)
            {
                _ui.Actions.OpenKeyPage(page);
                Report(null);
            }
            else
            {
                Report("Copied. Add the key in your repository host's settings.");
            }
        }
        catch (Exception error)
        {
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

    /// <summary>Trusts the host key that waits to be accepted; does nothing when there is none.</summary>
    public async Task AcceptHostKeyAsync()
    {
        if (_ui.Sync.Setup.State.HostKey is { CanAccept: true } host)
        {
            await _ui.RunAsync(token => _ui.Sync.Setup.AcceptHostKeyAsync(host.Fingerprint, token), Report);
        }
    }

    /// <summary>Forgets the host key; does nothing when there is none.</summary>
    public async Task ForgetHostKeyAsync()
    {
        if (_ui.Sync.Setup.State.HostKey is { } host)
        {
            await _ui.RunAsync(token => _ui.Sync.Setup.ForgetHostKeyAsync(host.Host, host.Port, token), Report);
        }
    }

    private void Report(string? text)
    {
        Message = text;
        _changed();
    }
}
