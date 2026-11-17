using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Changing the repository: the lines that show the current one, the choice to clone one or to use a clone the user
/// already has, and the change itself. The change hides the controls at once, runs one sync after it, and shows the
/// controls again, with why, when it fails.
/// </summary>
internal sealed class RepositoryChange
{
    private readonly SyncUi _ui;
    private readonly Action _changed;
    private bool _readFailed;

    /// <param name="ui">The window's sync service and operations.</param>
    /// <param name="changed">Called whenever something shown here changed, so the window shows it.</param>
    public RepositoryChange(SyncUi ui, Action changed)
    {
        _ui = ui;
        _changed = changed;
    }

    /// <summary>The change the user chose, or none yet.</summary>
    public RepositoryChoice Choice { get; private set; }

    /// <summary>Whether the address and folder boxes, and the button of the choice, show.</summary>
    public bool ControlsVisible { get; private set; }

    /// <summary>What to tell the user, or null when there is nothing to say.</summary>
    public string? Message { get; private set; }

    /// <summary>The repository, folder, and identity the setup held at the last <see cref="Refresh"/>.</summary>
    public RepositoryLines Lines { get; private set; }

    /// <summary>Reads the lines from the setup. A failure to read them becomes the message.</summary>
    public void Refresh()
    {
        try
        {
            Lines = SyncText.Repository(_ui.Sync.Setup.State);
            if (_readFailed)
            {
                _readFailed = false;
                Message = null;
            }
        }
        catch (Exception error)
        {
            _readFailed = true;
            Message = $"The repository couldn't be read: {error.Message}";
        }

        _changed();
    }

    /// <summary>Shows the controls of a choice and clears the message of an earlier attempt.</summary>
    public void Choose(RepositoryChoice choice)
    {
        Choice = choice;
        ControlsVisible = true;
        Message = null;
        _changed();
    }

    public bool CanSetKey(string address) => _ui.Sync.Setup.CheckAddress(address.Trim(), out _);

    public void SetKey(string address)
    {
        var setup = _ui.Sync.Setup;
        if (!setup.CheckAddress(address.Trim(), out var page))
        {
            return;
        }

        if (setup.State.PublicKey is not { } key)
        {
            Show(DeviceActions.NoKeyMessage);
            return;
        }

        try
        {
            _ui.Actions.CopyPublicKey(key);
            if (page is null)
            {
                Show("Copied. Add the key in your repository host's settings.");
            }
            else
            {
                _ui.Actions.OpenKeyPage(page);
                Show(null);
            }
        }
        catch (Exception error)
        {
            Show(error.Message);
        }
    }

    /// <summary>
    /// Clones or uses the clone, by the choice, then runs one sync. Missing input changes nothing. A failure shows
    /// the controls again.
    /// </summary>
    public async Task ChangeAsync(string address, string folder)
    {
        if (Choice == RepositoryChoice.None)
        {
            throw new InvalidOperationException("Choose to clone a repository or to use an existing clone first.");
        }

        address = address.Trim();
        folder = folder.Trim();
        if (address.Length == 0 || folder.Length == 0)
        {
            Show("Enter the repository's address and the local folder.");
            return;
        }

        ControlsVisible = false;
        Show(null);
        var choice = Choice;
        var failed = false;
        await _ui.RunAsync(
            async token =>
            {
                var setup = _ui.Sync.Setup;
                await (choice == RepositoryChoice.Clone
                    ? setup.CloneAsync(address, folder, token)
                    : setup.UseExistingAsync(address, folder, token));
                await _ui.SyncNowAsync(Show, token);
            },
            error =>
            {
                failed = true;
                Show(error);
            });
        if (failed)
        {
            ControlsVisible = true;
            _changed();
        }
    }

    private void Show(string? message)
    {
        Message = message;
        _changed();
    }
}
