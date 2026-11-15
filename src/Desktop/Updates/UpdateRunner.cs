namespace Memoria.ToDo.Desktop.Updates;

/// <summary>
/// Runs the checks and the updates: a check at start and each night, an update when asked or, with Auto update on, at night
/// once no window is open, and the stop that cancels what is under way and starts Velopack's updater last.
/// </summary>
internal sealed class UpdateRunner(IAppInstall install, UpdateModel model, NightSchedule night, UiThread ui)
{
    private readonly CancellationTokenSource _stop = new();
    private IAppExit? _exit;
    private Task _running = Task.CompletedTask;
    private Task _updating = Task.CompletedTask;
    private Task _nightInstall = Task.CompletedTask;
    private (NewerVersion Version, bool Quiet)? _applyOnStop;
    private Task? _stopping;

    /// <summary>Reads Auto update and begins the checks without waiting for the first one. Does nothing when the app isn't installed.</summary>
    public void Start(IAppExit exit)
    {
        ArgumentNullException.ThrowIfNull(exit);
        if (_exit is not null)
        {
            throw new InvalidOperationException("Updates has started already.");
        }

        _exit = exit;
        if (!install.IsInstalled)
        {
            return;
        }

        model.ReadAutoUpdate();
        _running = RunAsync(_stop.Token);
    }

    /// <summary>Starts an update when a version is available.</summary>
    public void UpdateNow()
    {
        if (_exit is not null && model.TryBeginUpdate() is { } version)
        {
            _updating = UpdateAsync(version, quiet: false, _stop.Token);
        }
    }

    /// <summary>Cancels what is under way and waits for it; then starts the updater when an update ended the app. Repeated calls join one stop.</summary>
    public Task StopAsync() => _stopping ??= StopCoreAsync();

    private async Task StopCoreAsync()
    {
        await _stop.CancelAsync();
        await _running;
        await _updating;
        await _nightInstall;
        if (_applyOnStop is { } apply)
        {
            install.ApplyAfterExit(apply.Version, apply.Quiet);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CheckAsync(cancellationToken);
            while (true)
            {
                await night.WaitForNextNightAsync(cancellationToken);
                await CheckAsync(cancellationToken);

                // The install waits for windows apart from the checks, so they go on each night; one that waits already installs the latest found.
                if (_nightInstall.IsCompleted && model.AutoUpdate && model.Stage == UpdateStage.Available)
                {
                    _nightInstall = InstallWhenNoWindowAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stop ended the checks.
        }
    }

    // Gives up for good when Auto update is turned off while it waits, even if it is turned on again before the next look.
    // A download that has begun goes on, as an update you started would.
    private async Task InstallWhenNoWindowAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await WaitForNoWindowAsync(cancellationToken))
            {
                return;
            }

            if (model.AutoUpdate && model.TryBeginUpdate() is { } version)
            {
                await UpdateAsync(version, quiet: true, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stop ended the wait.
        }
    }

    /// <returns>Whether no window is open; false when Auto update was turned off meanwhile.</returns>
    private async Task<bool> WaitForNoWindowAsync(CancellationToken cancellationToken)
    {
        using var turnedOff = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        void CancelWhenOff(object? sender, EventArgs e)
        {
            if (!model.AutoUpdate)
            {
                turnedOff.Cancel();
            }
        }

        ui.Run(() => model.Changed += CancelWhenOff);
        try
        {
            await night.WaitUntilAsync(() => ui.Run(() => !model.AutoUpdate || !_exit!.AnyWindowOpen), turnedOff.Token);
            return true;
        }
        catch (OperationCanceledException) when (turnedOff.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            ui.Run(() => model.Changed -= CancelWhenOff);
        }
    }

    // A failed check changes nothing and shows nothing. Velopack's check can't be cancelled, so a stop stops waiting for it.
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        if (model.Stage == UpdateStage.Updating)
        {
            return;
        }

        NewerVersion? found;
        try
        {
            found = await install.CheckAsync(cancellationToken).WaitAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return;
        }

        model.CheckFound(found);
    }

    /// <returns>Whether the app was ended, so the updater starts at the stop.</returns>
    private async Task<bool> UpdateAsync(NewerVersion version, bool quiet, CancellationToken cancellationToken)
    {
        try
        {
            await install.DownloadAsync(version, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            model.EndUpdate(null);
            return false;
        }
        catch (Exception e)
        {
            model.EndUpdate($"The update to {version.Version} failed: {e.Message}");
            return false;
        }

        try
        {
            // A window may have opened during a quiet download; the restart waits until every window is closed again.
            if (quiet)
            {
                await night.WaitUntilAsync(() => ui.Run(() => !_exit!.AnyWindowOpen), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            model.EndUpdate(null);
            return false;
        }

        if (!ui.Run(_exit!.CloseWindows))
        {
            model.EndUpdate(null);
            return false;
        }

        _applyOnStop = (version, quiet);
        ui.Run(_exit.End);
        return true;
    }
}
