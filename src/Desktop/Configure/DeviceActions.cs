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

    public void CopyPublicKey()
    {
        var key = _ui.Sync.Setup.State.PublicKey;
        Report(key is null ? "Generate a key first." : null);
        if (key is not null)
        {
            Try(() => _ui.Actions.CopyPublicKey(key));
        }
    }

    public void OpenKeyPage()
    {
        var page = _ui.Sync.Setup.State.AddKeyPage;
        Report(page is null ? "The repository's host has no page to add a key." : null);
        if (page is not null)
        {
            Try(() => _ui.Actions.OpenKeyPage(page));
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

    private void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            Report(error.Message);
        }
    }

    private void Report(string? text)
    {
        Message = text;
        _changed();
    }
}
