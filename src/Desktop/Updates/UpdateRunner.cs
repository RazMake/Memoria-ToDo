using Serilog;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>
/// Runs the checks and the updates: a check at start and each night, an update when asked or, with Auto update on, at night
/// once no window is open, and the stop that cancels what is under way and starts Velopack's updater last.
/// Logs each check and its result, each download's start and end, the restart, and the failure of any of them.
/// </summary>
internal sealed class UpdateRunner(IAppInstall install, UpdateModel model, NightSchedule night, UiThread ui, ILogger logger)
{
    private readonly CancellationTokenSource _stop = new();
    private IAppExit? _exit;
    private Task _running = Task.CompletedTask;
    private Task _updating = Task.CompletedTask;
    private Task _nightInstall = Task.CompletedTask;
    private (NewerVersion Version, bool Quiet)? _applyOnStop;
    private Task? _stopping;
    private readonly object _checkLock = new();
    private Task<string?>? _check;

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
            logger.Information("Restarting into version {Version}", apply.Version.Version);
            // A failure to restart goes on to AppRunner, which logs it as Fatal.
            install.ApplyAfterExit(apply.Version, apply.Quiet);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CheckAsync(cancellationToken, BySchedule);
            while (true)
            {
                await night.WaitForNextNightAsync(cancellationToken);
                await CheckAsync(cancellationToken, BySchedule);

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

    /// <summary>
    /// Checks now, as a scheduled check does, joining one under way. Returns null when it succeeded or when nothing could check,
    /// and the failure's message when it failed. A stop ends the wait with an <see cref="OperationCanceledException"/>.
    /// </summary>
    public Task<string?> CheckNowAsync()
    {
        if (_exit is null || model.Stage is not (UpdateStage.NoUpdate or UpdateStage.Available))
        {
            return Task.FromResult<string?>(null);
        }

        return CheckAsync(_stop.Token, ByCheckForUpdates);
    }

    private const string ByCheckForUpdates = "Check for updates";
    private const string BySchedule = "the schedule";

    // One check runs at a time, and a caller that comes meanwhile gets its outcome rather than a second request.
    private Task<string?> CheckAsync(CancellationToken cancellationToken, string origin)
    {
        lock (_checkLock)
        {
            if (_check is { IsCompleted: false } running)
            {
                logger.Information("A check is under way already, so this call from {Origin} waits for it", origin);
                return running;
            }

            return _check = CheckCoreAsync(cancellationToken, origin);
        }
    }

    // A failed check changes nothing and shows nothing, but is logged. Velopack's check can't be cancelled, so a stop stops waiting for it.
    private async Task<string?> CheckCoreAsync(CancellationToken cancellationToken, string origin)
    {
        if (model.Stage == UpdateStage.Updating)
        {
            return null;
        }

        NewerVersion? found;
        try
        {
            found = await install.CheckAsync(cancellationToken).WaitAsync(cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.Warning(e, "The check for a newer version, started by {Origin}, failed", origin);
            return e.Message;
        }

        if (found is null)
        {
            logger.Information("The check, started by {Origin}, found no newer version", origin);
        }
        else
        {
            logger.Information("The check, started by {Origin}, found version {Version}", origin, found.Version);
        }

        model.CheckFound(found);
        return null;
    }

    /// <returns>Whether the app was ended, so the updater starts at the stop.</returns>
    private async Task<bool> UpdateAsync(NewerVersion version, bool quiet, CancellationToken cancellationToken)
    {
        logger.Information("Downloading version {Version}", version.Version);
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
            logger.Error(e, "The download of version {Version} failed", version.Version);
            model.EndUpdate($"The update to {version.Version} failed: {e.Message}");
            return false;
        }

        logger.Information("Downloaded version {Version}", version.Version);
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
